using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class CloudResourceService : ICloudResourceService
{
    private readonly IConfiguration _configuration;
    private readonly ISecurityComplianceService _securityComplianceService;

    public CloudResourceService(IConfiguration configuration, ISecurityComplianceService securityComplianceService)
    {
        _configuration = configuration;
        _securityComplianceService = securityComplianceService;
    }

    public async Task<CloudResourceDashboardViewModel> GetDashboardDataAsync()
    {
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
            throw new Exception("CRITICAL: Không thể kết nối với Vùng Singapore (ap-southeast-1). Mất kết nối diện rộng!");
        }

        var viewModel = new CloudResourceDashboardViewModel
        {
            S3BucketName = bucketName,
            DynamoDbTableName = tableName,
            LambdaFunctionName = lambdaName,
            LatestMigrationId = "N/A",
            LatestRestoreStatus = "N/A",
            RestoreAvailable = false
        };

        // Initialize Client Configurations
        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = region,
            Timeout = TimeSpan.FromSeconds(3)
        };
        using var s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region,
            Timeout = TimeSpan.FromSeconds(3)
        };
        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region,
            Timeout = TimeSpan.FromSeconds(3)
        };
        using var lambdaClient = new AmazonLambdaClient(accessKey, secretKey, lambdaConfig);

        int healthScore = 0;
        bool hasPassedSelfTest = false;

        // 1. Fetch S3 Backups
        try
        {
            var s3Response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucketName });
            healthScore += 25;

            if (s3Response.S3Objects != null && s3Response.S3Objects.Any())
            {
                healthScore += 25;
                viewModel.BackupFilesCount = s3Response.S3Objects.Count;

                viewModel.RecentBackups = s3Response.S3Objects
                    .Select(o => new S3ObjectViewModel
                    {
                        Key = o.Key,
                        Size = o.Size.GetValueOrDefault(),
                        LastModified = o.LastModified.GetValueOrDefault()
                    })
                    .OrderByDescending(o => o.LastModified)
                    .Take(10)
                    .ToList();

                // Find the latest Migration ID
                string latestMigId = "N/A";
                DateTime latestMigTime = DateTime.MinValue;
                foreach (var obj in s3Response.S3Objects)
                {
                    var parts = obj.Key.Split('/');
                    if (parts.Length > 1 && parts[0] == "migrations" && parts[1].StartsWith("MIGRATION_"))
                    {
                        var migrationId = parts[1];
                        if (obj.LastModified.GetValueOrDefault() > latestMigTime)
                        {
                            latestMigTime = obj.LastModified.GetValueOrDefault();
                            latestMigId = migrationId;
                        }
                    }
                }
                viewModel.LatestMigrationId = latestMigId;
                viewModel.RestoreAvailable = latestMigId != "N/A";
            }
        }
        catch (Exception ex)
        {
            viewModel.IsLocalStackAvailable = false;
            viewModel.S3ErrorMessage = $"Không kết nối được S3 Bucket '{bucketName}' trên LocalStack: {ex.Message}";
        }

        // 2. Fetch DynamoDB Logs
        try
        {
            var scanResponse = await dynamoClient.ScanAsync(new ScanRequest { TableName = tableName });
            healthScore += 25;

            if (scanResponse.Items != null && scanResponse.Items.Any())
            {
                var logs = scanResponse.Items.Select(item => new MigrationLogItem
                {
                    MigrationId = GetAttributeString(item, "MigrationId"),
                    Timestamp = GetAttributeString(item, "Timestamp"),
                    Status = GetAttributeString(item, "Status"),
                    Message = GetAttributeString(item, "Message")
                }).ToList();

                viewModel.MigrationLogsCount = logs.Count;
                viewModel.RecentLogs = logs
                    .OrderByDescending(l => l.Timestamp)
                    .Take(15)
                    .ToList();

                // Check for SELF_TEST_PASSED or SELF_TEST_TRIGGERED log status for 25 points
                if (logs.Any(l => l.Status == "SELF_TEST_PASSED" || l.Status == "SELF_TEST_TRIGGERED"))
                {
                    healthScore += 25;
                }

                if (logs.Any(l => l.Status == "SELF_TEST_PASSED"))
                {
                    hasPassedSelfTest = true;
                    viewModel.LatestSelfTestStatus = "Passed";
                }
                else if (logs.Any(l => l.Status == "SELF_TEST_TRIGGERED" || l.Status == "STARTED"))
                {
                    viewModel.LatestSelfTestStatus = "Running";
                }
                else if (logs.Any(l => l.Status == "FAILED"))
                {
                    viewModel.LatestSelfTestStatus = "Failed";
                }

                // Identify the latest Restore Simulation status
                var restoreLogs = logs
                    .Where(l => l.Status.StartsWith("RESTORE_"))
                    .OrderByDescending(l => l.Timestamp)
                    .ToList();

                if (restoreLogs.Any())
                {
                    var latestRestore = restoreLogs.First();
                    if (latestRestore.Status == "RESTORE_COMPLETED")
                    {
                        viewModel.LatestRestoreStatus = "Completed";
                    }
                    else if (latestRestore.Status == "RESTORE_WARNING")
                    {
                        viewModel.LatestRestoreStatus = "Warning";
                    }
                    else if (latestRestore.Status == "RESTORE_FAILED")
                    {
                        viewModel.LatestRestoreStatus = "Failed";
                    }
                    else if (latestRestore.Status == "RESTORE_STARTED")
                    {
                        viewModel.LatestRestoreStatus = "Running";
                    }
                    else
                    {
                        viewModel.LatestRestoreStatus = latestRestore.Status;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            viewModel.IsLocalStackAvailable = false;
            viewModel.DynamoDbErrorMessage = $"Không kết nối được DynamoDB Table '{tableName}' trên LocalStack: {ex.Message}";
        }

        // 3. Check Lambda connectivity
        try
        {
            await lambdaClient.GetFunctionAsync(new GetFunctionRequest { FunctionName = lambdaName });
        }
        catch (Exception ex)
        {
            if (DisasterState.IsFailedOver && region == "ap-northeast-1")
            {
                // Graceful fallback for DR simulation
            }
            else
            {
                viewModel.LambdaErrorMessage = $"Không kết nối hoặc định danh được Lambda Function '{lambdaName}': {ex.Message}";
            }
        }

        // Populate simulated cost list
        viewModel.CostItems = new List<CostItemViewModel>
        {
            new() { ServiceName = "S3 Storage", Description = "Lưu trữ backup tệp tin JSON giả lập", CostPerMonth = viewModel.BackupFilesCount > 0 ? 0.08 : 0.0 },
            new() { ServiceName = "DynamoDB Requests", Description = "Ghi logs trạng thái tiến trình di trú", CostPerMonth = viewModel.MigrationLogsCount > 0 ? 0.10 : 0.0 },
            new() { ServiceName = "Lambda Invocations", Description = "Kích hoạt kiểm thử tự động sau di trú", CostPerMonth = hasPassedSelfTest ? 0.05 : 0.0 },
            new() { ServiceName = "AWS Site-to-Site VPN", Description = "Kênh truyền bảo mật mã hóa nối On-premise và VPC", CostPerMonth = 0.05 },
            new() { ServiceName = "Local Development/Docker", Description = "Môi trường giả lập LocalStack không phát sinh chi phí", CostPerMonth = 0.00 },
            new() { ServiceName = "Network/Other", Description = "Chi phí truyền dữ liệu và dịch vụ khác", CostPerMonth = (viewModel.BackupFilesCount > 0 || viewModel.MigrationLogsCount > 0) ? 0.07 : 0.0 }
        };

        viewModel.EstimatedMonthlyCost = viewModel.CostItems.Sum(c => c.CostPerMonth);

        // Populate before/after comparison list
        viewModel.ComparisonItems = new List<ComparisonItemViewModel>
        {
            new() { Category = "Hạ tầng", OnPremiseDetails = "Cấu hình thủ công trên máy local/server vật lý", CloudDetails = "Hạ tầng mô tả bằng Terraform, có thể apply/destroy tự động", Icon = "bi-boxes" },
            new() { Category = "Lưu trữ file", OnPremiseDetails = "Lưu trên ổ đĩa máy chủ", CloudDetails = "Lưu trên S3 Object Storage", Icon = "bi-archive" },
            new() { Category = "Nhật ký hệ thống", OnPremiseDetails = "Log local hoặc SQL Server", CloudDetails = "Migration logs lưu trên DynamoDB", Icon = "bi-journal-text" },
            new() { Category = "Kết nối mạng & Bảo mật", OnPremiseDetails = "Mạng LAN nội bộ hoặc Internet công cộng, tường lửa thủ công", CloudDetails = "Mạng riêng ảo VPC (Public/Private Subnet), kết nối bảo mật qua IPSec VPN Site-to-Site", Icon = "bi-shield-shaded" },
            new() { Category = "Kiểm thử sau triển khai", OnPremiseDetails = "Kiểm tra thủ công", CloudDetails = "Lambda Self-test kiểm tra S3 và DynamoDB", Icon = "bi-lightning-charge" },
            new() { Category = "Khả năng khôi phục", OnPremiseDetails = "Phụ thuộc backup thủ công", CloudDetails = "Có backup object, log trạng thái và rollback simulation", Icon = "bi-arrow-counterclockwise" },
            new() { Category = "Chi phí", OnPremiseDetails = "Khó ước tính chi tiết theo từng dịch vụ", CloudDetails = "Có bảng ước tính chi phí theo từng thành phần", Icon = "bi-cash-stack" }
        };

        if (DisasterState.IsFailedOver && region == "ap-northeast-1")
        {
            viewModel.MigrationHealthScore = 100;
        }
        else
        {
            viewModel.MigrationHealthScore = healthScore;
        }

        try
        {
            var securityData = await _securityComplianceService.GetSecurityComplianceDataAsync();
            viewModel.SecurityComplianceScore = securityData.ComplianceScore;
        }
        catch
        {
            viewModel.SecurityComplianceScore = 0;
        }

        return viewModel;
    }

    private string GetAttributeString(Dictionary<string, AttributeValue> item, string key)
    {
        return item.TryGetValue(key, out var val) ? val.S : string.Empty;
    }
}
