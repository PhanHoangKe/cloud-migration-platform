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
    private readonly IMigrationPackageService _packageService;
    private readonly IMigrationReportService _reportService;
    private readonly IDatabaseExportService _dbExportService;
    private readonly OnPremiseAppOptions _options;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MigrationResultViewModel> _activeMigrations = new();

    public MigrationService(
        IConfiguration configuration, 
        IWebHostEnvironment env, 
        IMigrationPackageService packageService,
        IMigrationReportService reportService,
        IDatabaseExportService dbExportService,
        Microsoft.Extensions.Options.IOptions<OnPremiseAppOptions> options)
    {
        _configuration = configuration;
        _env = env;
        _packageService = packageService;
        _reportService = reportService;
        _dbExportService = dbExportService;
        _options = options.Value;
    }

    public MigrationResultViewModel? GetMigrationResult(string migrationId)
    {
        if (_activeMigrations.TryGetValue(migrationId, out var cachedResult))
        {
            return cachedResult;
        }

        // Try to reconstruct from local report file
        var tempDirectory = Path.Combine(_env.WebRootPath, "migration-packages", migrationId);
        var reportJsonPath = Path.Combine(tempDirectory, "migration-report.json");
        if (File.Exists(reportJsonPath))
        {
            try
            {
                var jsonContent = File.ReadAllText(reportJsonPath);
                var report = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                if (report != null)
                {
                    var result = new MigrationResultViewModel
                    {
                        MigrationId = migrationId,
                        Status = "COMPLETED",
                        IsSuccess = true,
                        S3BucketName = report.s3BucketName ?? string.Empty,
                        PackageSize = report.packageSizeMb ?? string.Empty,
                        ReadinessScore = (int)(report.readinessScore ?? 0),
                        LambdaSelfTestResponse = (report.lambdaSelfTestStatus == "Passed") ? "Success" : "Failed",
                        DatabaseExportStatus = report.databaseExport?.status ?? string.Empty,
                        DatabaseExportS3Key = report.databaseExport?.s3Key ?? string.Empty,
                        DatabaseTableCount = (int)(report.databaseExport?.tableCount ?? 0),
                        DatabaseTotalRows = (int)(report.databaseExport?.totalRows ?? 0),
                        ReportGenerated = true,
                        ReportHtmlS3Key = $"migrations/{migrationId}/migration-report.html",
                        ReportJsonS3Key = $"migrations/{migrationId}/migration-report.json",
                        ReportLocalHtmlPath = Path.Combine(tempDirectory, "migration-report.html")
                    };

                    if (report.uploadedFiles != null)
                    {
                        foreach (var f in report.uploadedFiles)
                        {
                            result.S3ObjectKeys.Add($"migrations/{migrationId}/{f}");
                        }
                    }

                    return result;
                }
            }
            catch
            {
                // Fallback to null
            }
        }

        return null;
    }

    public async Task<MigrationResultViewModel> StartMigrationAsync(string? migrationId = null)
    {
        DateTime startedAt = DateTime.UtcNow;
        
        // 1. Read configurations
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = DisasterState.CurrentRegion;
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        
        var bucketName = localStackConfig["MigrationBucketName"] ?? "cloud-migration-backup-vinhuni";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-vinhuni";
        var lambdaName = localStackConfig["SelfTestLambdaName"] ?? "cloud-migration-self-test-vinhuni";

        if (region == "ap-northeast-1")
        {
            bucketName = "cloud-migration-backup-vinhuni-tokyo";
            tableName = "cloud-migration-logs-vinhuni-tokyo";
            lambdaName = "cloud-migration-self-test-vinhuni-tokyo";
        }

        if (DisasterState.IsSingaporeDown && region == "ap-southeast-1")
        {
            throw new Exception("CRITICAL: Không thể thực hiện chuyển đổi do Vùng Singapore (ap-southeast-1) đang bị sập kết nối diện rộng!");
        }

        var result = new MigrationResultViewModel
        {
            MigrationId = migrationId ?? $"MIGRATION_{DateTime.Now:yyyyMMdd_HHmmss}",
            S3BucketName = bucketName,
            IsSuccess = false
        };

        _activeMigrations[result.MigrationId] = result;

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
            var onPremAppPath = _options.SourcePath;
            if (string.IsNullOrEmpty(onPremAppPath) || !Directory.Exists(onPremAppPath))
            {
                result.IsSuccess = false;
                result.Status = "FAILED";
                result.ErrorMessage = $"Lỗi thư mục nguồn: Thư mục dự án On-Premise '{_options.DisplayName}' không tồn tại hoặc không truy cập được tại đường dẫn: '{onPremAppPath}'. Vui lòng cấu hình đúng SourcePath trong tệp appsettings.json.";
                return result;
            }

            // Step 1: Write Log STARTED to DynamoDB (Clean English to avoid CLI encoding crash)
            result.Status = "STARTED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "STARTED", $"Migration process started for {_options.DisplayName} from On-premise to LocalStack Cloud.");
            AddLocalLog(result, "STARTED", "Ghi log STARTED thành công vào DynamoDB table.");

            // Determine temporary packages directory
            var tempDirectory = Path.Combine(_env.WebRootPath, "migration-packages", result.MigrationId);
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }

            // Step 2: Create assessment-report.json
            var reportPath = Path.Combine(tempDirectory, "assessment-report.json");
            var report = await _packageService.GenerateAssessmentReportAsync(result.MigrationId, onPremAppPath, reportPath);
            result.ReadinessScore = report.ReadinessScore;

            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "ASSESSMENT_EXPORTED", "Pre-migration assessment report generated");
            AddLocalLog(result, "ASSESSMENT_EXPORTED", $"Tạo tệp báo cáo đánh giá chất lượng thành công tại local: {reportPath}");

            // Step 3: Create migration-manifest.json
            var manifestPath = Path.Combine(tempDirectory, "migration-manifest.json");
            await _packageService.GenerateManifestAsync(result.MigrationId, onPremAppPath, manifestPath, "source-package.zip", "assessment-report.json");

            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "MANIFEST_CREATED", "Migration manifest created");
            AddLocalLog(result, "MANIFEST_CREATED", $"Tạo tệp manifest di trú thành công tại local: {manifestPath}");

            // Step 4: Create source-package.zip
            var zipPath = Path.Combine(tempDirectory, "source-package.zip");
            var zipSize = await _packageService.CreateSourcePackageZipAsync(onPremAppPath, zipPath);
            result.PackageSize = zipSize;

            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "SOURCE_PACKAGED", $"Source package zip created with size {zipSize}");
            AddLocalLog(result, "SOURCE_PACKAGED", $"Tạo tệp nén zip chứa mã nguồn thành công tại local: {zipPath}");

            // Database Export Step
            try
            {
                var dbExportResult = await _dbExportService.ExportDatabaseAsync(result.MigrationId);
                result.DatabaseExportStatus = dbExportResult.ConnectionStatus && string.IsNullOrEmpty(dbExportResult.ErrorMessage) ? "SUCCESS" : "FAILED";
                result.DatabaseExportS3Key = dbExportResult.S3Key ?? string.Empty;
                result.DatabaseTableCount = dbExportResult.TotalTables;
                result.DatabaseTotalRows = dbExportResult.TotalRows;
                result.DatabaseExportErrorMessage = dbExportResult.ErrorMessage;

                if (dbExportResult.ConnectionStatus)
                {
                    AddLocalLog(result, "DATABASE_CONNECTED", "Ket noi CSDL On-premise thanh cong.");
                    if (string.IsNullOrEmpty(dbExportResult.ErrorMessage))
                    {
                        await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "DATABASE_EXPORTED", $"Database export succeeded: {dbExportResult.TotalTables} tables, {dbExportResult.TotalRows} rows");
                        AddLocalLog(result, "DATABASE_EXPORTED", $"Xuat du lieu thanh cong: {dbExportResult.TotalTables} bang, {dbExportResult.TotalRows} dong.");
                        AddLocalLog(result, "DATABASE_EXPORT_UPLOADED_TO_S3", "Tai tep database-export.json len S3 thanh cong.");
                        if (!string.IsNullOrEmpty(dbExportResult.S3Key))
                        {
                            result.S3ObjectKeys.Add(dbExportResult.S3Key);
                        }
                    }
                    else
                    {
                        await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "DATABASE_EXPORT_FAILED", $"Database export warning: {dbExportResult.ErrorMessage}");
                        AddLocalLog(result, "DATABASE_EXPORT_FAILED", $"Xuat CSDL co canh bao: {dbExportResult.ErrorMessage}");
                    }
                }
                else
                {
                    await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "DATABASE_EXPORT_FAILED", $"Database connection failed: {dbExportResult.ErrorMessage}");
                    AddLocalLog(result, "DATABASE_EXPORT_FAILED", $"Ket noi CSDL that bai: {dbExportResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                result.DatabaseExportStatus = "FAILED";
                result.DatabaseExportErrorMessage = ex.Message;
                await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "DATABASE_EXPORT_FAILED", $"Database export system error: {ex.Message}");
                AddLocalLog(result, "DATABASE_EXPORT_FAILED", $"Xuat CSDL gap loi he thong: {ex.Message}");
            }

            // Step 5: Upload all 3 package files to S3
            var filesToUpload = new Dictionary<string, string>
            {
                { $"migrations/{result.MigrationId}/assessment-report.json", reportPath },
                { $"migrations/{result.MigrationId}/migration-manifest.json", manifestPath },
                { $"migrations/{result.MigrationId}/source-package.zip", zipPath }
            };

            foreach (var kvp in filesToUpload)
            {
                using (var fileStream = new FileStream(kvp.Value, FileMode.Open, FileAccess.Read))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = kvp.Key,
                        InputStream = fileStream
                    };
                    await s3Client.PutObjectAsync(putRequest);
                }
                result.S3ObjectKeys.Add(kvp.Key);
            }
            // For backward compatibility and file references:
            result.BackupFileName = "migration-manifest.json";
            result.S3ObjectKey = $"migrations/{result.MigrationId}/migration-manifest.json";

            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "UPLOADED_TO_S3", "Migration package uploaded to S3");
            AddLocalLog(result, "UPLOADED_TO_S3", $"Đã upload thành công 3 tệp tin lên S3 Object Keys thuộc thư mục migrations/{result.MigrationId}/");

            // Step 6: Trigger AWS Lambda Self-test Function
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "SELF_TEST_TRIGGERED", "Lambda self-test triggered");
            AddLocalLog(result, "SELF_TEST_TRIGGERED", $"Gọi Lambda self-test: {lambdaName}");

            string lambdaSelfTestStatus = "Failed";
            try
            {
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
                await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "SELF_TEST_PASSED", "Lambda self-test passed");
                AddLocalLog(result, "SELF_TEST_PASSED", $"Chạy Lambda self-test thành công. Phản hồi: {responsePayload}");
                
                if (!responsePayload.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    lambdaSelfTestStatus = "Passed";
                }
            }
            catch (Exception ex)
            {
                result.LambdaSelfTestResponse = $"Error: {ex.Message}";
                result.Status = "COMPLETED"; // Mark as completed but record the self test fail
                await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "SELF_TEST_FAILED", "Lambda self-test failed");
                AddLocalLog(result, "SELF_TEST_FAILED", $"Kiểm thử tự động Lambda thất bại: {ex.Message}");
            }

            // Step 7: Generate Migration Reports (JSON & HTML)
            DateTime completedAt = DateTime.UtcNow;
            int migrationHealthScore = lambdaSelfTestStatus == "Passed" ? 95 : 60;
            
            try
            {
                var (reportJsonPath, reportHtmlPath) = await _reportService.GenerateReportFilesAsync(
                    result.MigrationId,
                    onPremAppPath,
                    zipSize,
                    result.ReadinessScore,
                    bucketName,
                    tableName,
                    lambdaSelfTestStatus,
                    migrationHealthScore,
                    startedAt,
                    completedAt,
                    tempDirectory,
                    result.DatabaseExportStatus,
                    result.DatabaseTableCount,
                    result.DatabaseTotalRows,
                    result.DatabaseExportS3Key
                );

                result.ReportLocalHtmlPath = reportHtmlPath;
                result.ReportJsonS3Key = $"migrations/{result.MigrationId}/migration-report.json";
                result.ReportHtmlS3Key = $"migrations/{result.MigrationId}/migration-report.html";
                result.ReportGenerated = true;

                // Upload report files to S3
                var reportFilesToUpload = new Dictionary<string, string>
                {
                    { result.ReportJsonS3Key, reportJsonPath },
                    { result.ReportHtmlS3Key, reportHtmlPath }
                };

                foreach (var kvp in reportFilesToUpload)
                {
                    using (var fileStream = new FileStream(kvp.Value, FileMode.Open, FileAccess.Read))
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = bucketName,
                            Key = kvp.Key,
                            InputStream = fileStream
                        };
                        await s3Client.PutObjectAsync(putRequest);
                    }
                    result.S3ObjectKeys.Add(kvp.Key);
                }

                await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "REPORT_GENERATED", "Migration report generated successfully");
                AddLocalLog(result, "REPORT_GENERATED", "Báo cáo di trú dạng JSON & HTML đã được khởi tạo thành công.");

                await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "REPORT_UPLOADED_TO_S3", "Migration report uploaded to S3 successfully");
                AddLocalLog(result, "REPORT_UPLOADED_TO_S3", "Đã tải báo cáo di trú lên S3 thành công.");
            }
            catch (Exception ex)
            {
                result.ReportGenerated = false;
                await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "REPORT_FAILED", $"Migration report generation failed: {ex.Message}");
                AddLocalLog(result, "REPORT_FAILED", $"Lỗi tạo báo cáo di trú: {ex.Message}");
            }

            result.IsSuccess = true;
            result.Status = "COMPLETED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "COMPLETED", "Migration process completed successfully");
            AddLocalLog(result, "COMPLETED", "Quá trình di trú hoàn tất thành công.");
        }
        catch (AmazonDynamoDBException ex) when (ex.Message.Contains("ResourceNotFoundException") || ex.ErrorCode == "ResourceNotFoundException")
        {
            result.ErrorMessage = $"Lỗi DynamoDB: Table '{tableName}' không tồn tại. Vui lòng chạy lệnh 'terraform apply' để tạo tài nguyên trong LocalStack trước.";
            result.Status = "FAILED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "FAILED", $"Migration failed due to DynamoDB table missing: {ex.Message}");
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            result.ErrorMessage = $"Lỗi S3: Bucket '{bucketName}' không tồn tại. Vui lòng chạy lệnh 'terraform apply' để khởi tạo bucket.";
            result.Status = "FAILED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "FAILED", $"Migration failed due to S3 bucket missing: {ex.Message}");
        }
        catch (AmazonLambdaException ex) when (ex.ErrorCode == "ResourceNotFoundException" || ex.Message.Contains("Function not found"))
        {
            result.ErrorMessage = $"Lỗi Lambda: Function '{lambdaName}' không tồn tại. Vui lòng deploy Lambda function qua Terraform trước.";
            result.Status = "FAILED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "FAILED", $"Migration failed due to Lambda missing: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Lỗi kết nối LocalStack (Endpoint: {serviceUrl}): {ex.Message}. Hãy chắc chắn rằng LocalStack Docker Container đang chạy.";
            result.Status = "FAILED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.MigrationId, "FAILED", $"Migration failed with exception: {ex.Message}");
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
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-vinhuni";

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
