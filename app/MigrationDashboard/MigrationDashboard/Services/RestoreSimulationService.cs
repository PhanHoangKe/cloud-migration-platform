using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class RestoreSimulationService : IRestoreSimulationService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly OnPremiseAppOptions _options;

    public RestoreSimulationService(IConfiguration configuration, IWebHostEnvironment env, Microsoft.Extensions.Options.IOptions<OnPremiseAppOptions> options)
    {
        _configuration = configuration;
        _env = env;
        _options = options.Value;
    }

    public async Task<RestoreResultViewModel> RunRestoreSimulationAsync()
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var bucketName = localStackConfig["MigrationBucketName"] ?? "cloud-migration-backup-kedep";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-kedep";

        var result = new RestoreResultViewModel
        {
            RestoreId = $"RESTORE_{DateTime.Now:yyyyMMdd_HHmmss}",
            SourceBucket = bucketName,
            RestoredAt = DateTime.Now,
            IsSuccess = false
        };

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

        try
        {
            // Step 1: Write log RESTORE_STARTED to DynamoDB
            result.RestoreStatus = "STARTED";
            await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_STARTED", "Restore simulation started");
            AddLocalLog(result, "RESTORE_STARTED", "Mô phỏng khôi phục sự cố bắt đầu.");

            // Step 2: Find the latest migration on S3 according to 'migrations/' prefix
            AddLocalLog(result, "S3_SCANNING", $"Quét S3 bucket '{bucketName}' với tiền tố 'migrations/'...");
            
            ListObjectsV2Response s3Response;
            try
            {
                s3Response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = "migrations/"
                });
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.RestoreStatus = "FAILED";
                result.ErrorMessage = $"Không kết nối được S3 Bucket '{bucketName}' trên LocalStack: {ex.Message}";
                
                await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_FAILED", $"Restore failed - S3 connection error: {ex.Message}");
                AddLocalLog(result, "RESTORE_FAILED", $"Lỗi kết nối S3: {ex.Message}");
                return result;
            }

            if (s3Response.S3Objects == null || !s3Response.S3Objects.Any())
            {
                result.IsSuccess = false;
                result.RestoreStatus = "FAILED";
                result.ErrorMessage = "Chưa có gói migration nào trên S3 để thực hiện mô phỏng khôi phục.";
                
                await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_FAILED", "Restore failed - No migration packages found in S3");
                AddLocalLog(result, "RESTORE_FAILED", "Không tìm thấy bất kỳ gói migration nào trên S3.");
                return result;
            }

            // Find the latest Migration ID based on object LastModified
            string latestMigrationId = string.Empty;
            DateTime latestTime = DateTime.MinValue;

            foreach (var obj in s3Response.S3Objects)
            {
                var parts = obj.Key.Split('/');
                if (parts.Length > 1 && parts[0] == "migrations" && parts[1].StartsWith("MIGRATION_"))
                {
                    var migrationId = parts[1];
                    if (obj.LastModified.GetValueOrDefault() > latestTime)
                    {
                        latestTime = obj.LastModified.GetValueOrDefault();
                        latestMigrationId = migrationId;
                    }
                }
            }

            if (string.IsNullOrEmpty(latestMigrationId))
            {
                result.IsSuccess = false;
                result.RestoreStatus = "FAILED";
                result.ErrorMessage = "Không tìm thấy thư mục migration ID hợp lệ dưới tiền tố 'migrations/'.";
                
                await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_FAILED", "Restore failed - Valid migration directory not found");
                AddLocalLog(result, "RESTORE_FAILED", "Không xác định được thư mục di trú hợp lệ.");
                return result;
            }

            result.MigrationId = latestMigrationId;
            AddLocalLog(result, "MIGRATION_FOUND", $"Tìm thấy gói di trú mới nhất: '{latestMigrationId}' (Thời gian chỉnh sửa cuối: {latestTime.ToLocalTime():yyyy-MM-dd HH:mm:ss})");

            // Define the target package files
            var expectedFiles = new[]
            {
                new { Name = "assessment-report.json", Required = true, Description = "Báo cáo đánh giá chất lượng On-premise" },
                new { Name = "migration-manifest.json", Required = true, Description = "Tệp Manifest định nghĩa hạ tầng đám mây đích" },
                new { Name = "source-package.zip", Required = true, Description = $"Gói mã nguồn nén ZIP dự án {_options.DisplayName}" },
                new { Name = "database-export.json", Required = false, Description = "Bản xuất dữ liệu SQL Server On-premise dạng JSON" },
                new { Name = "migration-report.json", Required = false, Description = "Báo cáo di trú dạng JSON chứa metadata chi phí" },
                new { Name = "migration-report.html", Required = false, Description = "Báo cáo di trú định dạng HTML tĩnh" }
            };

            var restoreFiles = new List<RestoreFileViewModel>();
            var validations = new List<RestoreValidationItemViewModel>();
            bool hasMissingRequired = false;
            bool hasMissingOptional = false;

            foreach (var item in expectedFiles)
            {
                var s3Key = $"migrations/{latestMigrationId}/{item.Name}";
                var s3Obj = s3Response.S3Objects.FirstOrDefault(o => o.Key == s3Key);
                bool exists = s3Obj != null;
                long size = s3Obj?.Size ?? 0;

                var fileVm = new RestoreFileViewModel
                {
                    FileName = item.Name,
                    S3Key = s3Key,
                    ExistsOnS3 = exists,
                    DownloadedLocally = false,
                    Size = size
                };
                restoreFiles.Add(fileVm);

                // Add validation item
                var valItem = new RestoreValidationItemViewModel
                {
                    CheckName = $"Kiểm tra tệp {item.Name}"
                };

                if (exists)
                {
                    valItem.Status = "PASSED";
                    valItem.Description = $"Tệp tin tồn tại trên S3, kích thước: {(size >= 1048576 ? $"{(size / 1048576.0):F2} MB" : $"{(size / 1024.0):F1} KB")}.";
                }
                else
                {
                    if (item.Required)
                    {
                        valItem.Status = "FAILED";
                        valItem.Description = $"LỖI: Thiếu tệp bắt buộc '{item.Name}'. {item.Description}.";
                        hasMissingRequired = true;
                    }
                    else
                    {
                        valItem.Status = "WARNING";
                        valItem.Description = $"CẢNH BÁO: Thiếu tệp tùy chọn '{item.Name}'. {item.Description}.";
                        hasMissingOptional = true;
                    }
                }
                validations.Add(valItem);
            }

            result.RestoredFiles = restoreFiles;
            result.ValidationResults = validations;

            // Log validation result to DynamoDB
            string validationMsg = hasMissingRequired ? "Validation failed - missing required files" :
                                   (hasMissingOptional ? "Validation passed with warnings - missing optional reports" : 
                                   "Validation completed successfully - all files present");

            await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_PACKAGE_VALIDATED", $"Migration package validated from S3: {validationMsg}");
            AddLocalLog(result, "RESTORE_PACKAGE_VALIDATED", $"Kết quả xác thực gói di trú: {(hasMissingRequired ? "Thất bại (Thiếu file bắt buộc)" : (hasMissingOptional ? "Hợp lệ kèm cảnh báo" : "Hợp lệ hoàn toàn"))}");

            // Step 3: Setup local restore directory
            var localRestoreDir = Path.Combine(_env.WebRootPath, "restore-simulations", latestMigrationId);
            if (!Directory.Exists(localRestoreDir))
            {
                Directory.CreateDirectory(localRestoreDir);
            }
            result.LocalRestorePath = $"/restore-simulations/{latestMigrationId}";

            // Step 4: Download files locally
            AddLocalLog(result, "DOWNLOAD_STARTED", "Bắt đầu tải các tệp tin từ S3 về local...");
            foreach (var file in restoreFiles)
            {
                if (!file.ExistsOnS3) continue;

                // For source-package.zip, only download if size < 50MB (52,428,800 bytes)
                if (file.FileName == "source-package.zip" && file.Size >= 52428800)
                {
                    file.DownloadedLocally = false;
                    file.LocalPath = string.Empty; // Marked as skipped on local disk
                    AddLocalLog(result, "DOWNLOAD_SKIPPED", $"Bỏ qua tải xuống {file.FileName} vì dung lượng lớn ({(file.Size / 1048576.0):F2} MB > 50 MB), nhưng đã xác nhận tệp tồn tại trên S3.");
                    continue;
                }

                try
                {
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = bucketName,
                        Key = file.S3Key
                    };
                    using var getResponse = await s3Client.GetObjectAsync(getRequest);
                    var localFilePath = Path.Combine(localRestoreDir, file.FileName);
                    
                    await getResponse.WriteResponseStreamToFileAsync(localFilePath, false, System.Threading.CancellationToken.None);
                    file.DownloadedLocally = true;
                    file.LocalPath = $"/restore-simulations/{latestMigrationId}/{file.FileName}";
                    AddLocalLog(result, "DOWNLOAD_SUCCESS", $"Tải thành công: {file.FileName} -> localPath: {file.LocalPath}");
                }
                catch (Exception ex)
                {
                    file.DownloadedLocally = false;
                    AddLocalLog(result, "DOWNLOAD_ERROR", $"Lỗi tải tệp '{file.FileName}': {ex.Message}");
                }
            }

            await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_FILES_DOWNLOADED", "Restore files downloaded locally");
            AddLocalLog(result, "RESTORE_FILES_DOWNLOADED", "Quá trình tải tệp tin về thư mục local hoàn tất.");

            // Step 5: Determine overall restore status
            if (hasMissingRequired)
            {
                result.RestoreStatus = "FAILED";
                result.IsSuccess = false;
                result.ErrorMessage = "Khôi phục thất bại do thiếu tệp tin bắt buộc trong gói di trú.";
            }
            else if (hasMissingOptional)
            {
                result.RestoreStatus = "WARNING";
                result.IsSuccess = true;
            }
            else
            {
                result.RestoreStatus = "COMPLETED";
                result.IsSuccess = true;
            }

            // Step 6: Create restore-summary.json in the restore folder
            var summaryData = new
            {
                restoreId = result.RestoreId,
                migrationId = result.MigrationId,
                restoredAt = result.RestoredAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                sourceBucket = result.SourceBucket,
                restoredFiles = restoreFiles.Select(f => new
                {
                    fileName = f.FileName,
                    s3Key = f.S3Key,
                    existsOnS3 = f.ExistsOnS3,
                    downloadedLocally = f.DownloadedLocally,
                    localPath = f.LocalPath,
                    size = f.Size
                }).ToList(),
                validationResults = validations.Select(v => new
                {
                    checkName = v.CheckName,
                    status = v.Status,
                    description = v.Description
                }).ToList(),
                restoreStatus = result.RestoreStatus,
                note = "Disaster recovery simulation only, no production data was overwritten."
            };

            var summaryJsonPath = Path.Combine(localRestoreDir, "restore-summary.json");
            var jsonString = JsonConvert.SerializeObject(summaryData, Formatting.Indented);
            await File.WriteAllTextAsync(summaryJsonPath, jsonString, Encoding.UTF8);
            AddLocalLog(result, "SUMMARY_EXPORTED", $"Xuất tệp restore-summary.json thành công tại: /restore-simulations/{latestMigrationId}/restore-summary.json");

            // Step 7: Log completed to DynamoDB
            string finalStatus = result.RestoreStatus == "COMPLETED" ? "completed successfully" : 
                                 (result.RestoreStatus == "WARNING" ? "completed with warnings" : "failed due to validation errors");
            
            await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, $"RESTORE_{result.RestoreStatus}", $"Restore simulation {finalStatus}");
            AddLocalLog(result, $"RESTORE_{result.RestoreStatus}", $"Mô phỏng khôi phục hoàn thành với trạng thái: {result.RestoreStatus}");
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.RestoreStatus = "FAILED";
            result.ErrorMessage = $"Lỗi hệ thống trong khi chạy mô phỏng khôi phục: {ex.Message}";
            
            await LogToDynamoDbAsync(dynamoClient, tableName, result.RestoreId, "RESTORE_FAILED", $"Restore failed with system exception: {ex.Message}");
            AddLocalLog(result, "RESTORE_FAILED", $"Lỗi hệ thống: {ex.Message}");
        }

        return result;
    }

    private async Task LogToDynamoDbAsync(IAmazonDynamoDB client, string tableName, string restoreId, string status, string message)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "MigrationId", new AttributeValue { S = restoreId } },
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
            // Suppress logging errors to preserve main exceptions
        }
    }

    private void AddLocalLog(RestoreResultViewModel result, string status, string message)
    {
        result.Logs.Add(new MigrationLogItem
        {
            MigrationId = result.RestoreId,
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = status,
            Message = message
        });
    }
}
