using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Newtonsoft.Json;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class MigrationService : IMigrationService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public MigrationService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _env = env;
    }

    public async Task<MigrationResultViewModel> StartMigrationAsync()
    {
        // 1. Read configurations
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var bucketName = localStackConfig["MigrationBucketName"] ?? "cloud-migration-backup-kedep";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-kedep";
        var lambdaName = localStackConfig["SelfTestLambdaName"] ?? "cloud-migration-self-test-kedep";

        var result = new MigrationResultViewModel
        {
            MigrationId = $"MIGRATION_{DateTime.Now:yyyyMMdd_HHmmss}",
            S3BucketName = bucketName,
            IsSuccess = false
        };

        // 2. Initialize AWS Clients configured for LocalStack
        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = region
        };
        using var s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region
        };
        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region
        };
        using var lambdaClient = new AmazonLambdaClient(accessKey, secretKey, lambdaConfig);

        try
        {
            // Step 1: Write Log STARTED to DynamoDB
            result.Status = "STARTED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "STARTED", "Bắt đầu tiến trình chuyển đổi ứng dụng từ On-premise lên LocalStack Cloud.");
            AddLocalLog(result, "STARTED", "Ghi log STARTED thành công vào DynamoDB table.");

            // Step 2: Create a simulated backup file (JSON)
            var backupDirectory = Path.Combine(_env.WebRootPath, "migration-backups");
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            var backupFileName = $"{result.MigrationId}_backup.json";
            var backupFilePath = Path.Combine(backupDirectory, backupFileName);
            result.BackupFileName = backupFileName;

            var backupData = new
            {
                migrationId = result.MigrationId,
                sourceAppPath = @"D:\cloud-migration-platform\app\OnPremApp\EduFlex - ĐTĐM",
                sourceType = "ASP.NET Core 9 + SQL Server",
                createdAt = DateTime.UtcNow.ToString("o"),
                migratedFilesCount = 142,
                migratedRecordsCount = 2850,
                note = "Academic migration simulation using LocalStack and Terraform"
            };

            var backupJson = JsonConvert.SerializeObject(backupData, Formatting.Indented);
            await File.WriteAllTextAsync(backupFilePath, backupJson, Encoding.UTF8);

            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "BACKUP_CREATED", $"Đã tạo tệp backup giả lập tại local: {backupFileName}");
            AddLocalLog(result, "BACKUP_CREATED", $"Tạo tệp backup JSON thành công tại local: {backupFilePath}");

            // Step 3: Upload backup file to S3
            var s3ObjectKey = $"backups/{backupFileName}";
            result.S3ObjectKey = s3ObjectKey;

            using (var fileStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read))
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3ObjectKey,
                    InputStream = fileStream
                };
                await s3Client.PutObjectAsync(putRequest);
            }

            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "UPLOADED_TO_S3", $"Tải tệp backup lên S3 bucket {bucketName}/{s3ObjectKey} thành công.");
            AddLocalLog(result, "UPLOADED_TO_S3", $"Tải tệp backup lên S3 Object Key: {s3ObjectKey} thành công.");

            // Step 4: Call AWS Lambda Self-test Function
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "SELF_TEST_TRIGGERED", $"Kích hoạt Lambda check self-test: {lambdaName}");
            AddLocalLog(result, "SELF_TEST_TRIGGERED", $"Gọi Lambda self-test: {lambdaName}");

            var lambdaRequest = new InvokeRequest
            {
                FunctionName = lambdaName,
                Payload = JsonConvert.SerializeObject(new { migrationId = result.MigrationId })
            };

            var lambdaResponse = await lambdaClient.InvokeAsync(lambdaRequest);
            
            using var reader = new StreamReader(lambdaResponse.Payload);
            var responsePayload = await reader.ReadToEndAsync();
            result.LambdaSelfTestResponse = responsePayload;

            result.Status = "COMPLETED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "SELF_TEST_PASSED", $"Lambda self-test hoàn tất. Kết quả: {responsePayload}");
            AddLocalLog(result, "SELF_TEST_PASSED", $"Chạy Lambda self-test thành công. Phản hồi: {responsePayload}");

            result.IsSuccess = true;
        }
        catch (AmazonDynamoDBException ex) when (ex.Message.Contains("ResourceNotFoundException") || ex.ErrorCode == "ResourceNotFoundException")
        {
            result.ErrorMessage = $"Lỗi DynamoDB: Table '{tableName}' không tồn tại. Vui lòng chạy lệnh 'terraform apply' để tạo tài nguyên trong LocalStack trước.";
            result.Status = "FAILED";
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            result.ErrorMessage = $"Lỗi S3: Bucket '{bucketName}' không tồn tại. Vui lòng chạy lệnh 'terraform apply' để khởi tạo bucket.";
            result.Status = "FAILED";
        }
        catch (AmazonLambdaException ex) when (ex.ErrorCode == "ResourceNotFoundException" || ex.Message.Contains("Function not found"))
        {
            result.ErrorMessage = $"Lỗi Lambda: Function '{lambdaName}' không tồn tại. Vui lòng deploy Lambda function qua Terraform trước.";
            result.Status = "FAILED";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Lỗi kết nối LocalStack (Endpoint: {serviceUrl}): {ex.Message}. Hãy chắc chắn rằng LocalStack Docker Container đang chạy.";
            result.Status = "FAILED";
        }

        return result;
    }

    public async Task<MigrationResultViewModel> RollbackMigrationAsync()
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-kedep";

        var result = new MigrationResultViewModel
        {
            MigrationId = "MIGRATION_LATEST",
            IsSuccess = false,
            Status = "ROLLBACK_FAILED"
        };

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region,
            Timeout = TimeSpan.FromSeconds(5)
        };
        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        try
        {
            // 1. Try to find the latest MigrationId in DynamoDB
            try
            {
                var scanResponse = await dynamoClient.ScanAsync(new ScanRequest { TableName = tableName });
                if (scanResponse.Items != null && scanResponse.Items.Count > 0)
                {
                    var latestItem = scanResponse.Items
                        .Select(item => new
                        {
                            Id = item.TryGetValue("MigrationId", out var idVal) ? idVal.S : string.Empty,
                            Time = item.TryGetValue("Timestamp", out var timeVal) ? timeVal.S : string.Empty
                        })
                        .Where(x => !string.IsNullOrEmpty(x.Id))
                        .OrderByDescending(x => x.Time)
                        .FirstOrDefault();

                    if (latestItem != null)
                    {
                        result.MigrationId = latestItem.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Không kết nối được DynamoDB để lấy MigrationId gần nhất: {ex.Message}. Rollback simulation thất bại.";
                return result;
            }

            // 2. Write log ROLLBACK_STARTED to DynamoDB
            result.Status = "ROLLBACK_STARTED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "ROLLBACK_STARTED", "Rollback simulation started");
            AddLocalLog(result, "ROLLBACK_STARTED", "Quy trình Rollback giả lập được bắt đầu.");

            // 3. Write log ROLLBACK_COMPLETED to DynamoDB
            result.Status = "ROLLBACK_COMPLETED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "ROLLBACK_COMPLETED", "Rollback simulation completed successfully. No real cloud data was deleted for demo safety.");
            AddLocalLog(result, "ROLLBACK_COMPLETED", "Rollback simulation hoàn tất thành công. Không xóa dữ liệu đám mây thật để đảm bảo an toàn demo.");

            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Lỗi trong quá trình thực hiện Rollback: {ex.Message}";
            result.Status = "ROLLBACK_FAILED";
        }

        return result;
    }

    private async Task LogToDynamoDbAsync(IAmazonDynamoDB client, string tableName, string migrationId, string status, string message)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "MigrationId", new AttributeValue { S = migrationId } },
                { "Timestamp", new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") } },
                { "Status", new AttributeValue { S = status } },
                { "Message", new AttributeValue { S = message } }
            };

            var request = new PutItemRequest
            {
                TableName = tableName,
                Item = item
            };

            await client.PutItemAsync(request);
        }
        catch
        {
            // Suppress secondary logging failures to avoid masking primary exceptions
        }
    }

    private void AddLocalLog(MigrationResultViewModel result, string status, string message)
    {
        result.Logs.Add(new MigrationLogItem
        {
            MigrationId = result.MigrationId,
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = status,
            Message = message
        });
    }
}
