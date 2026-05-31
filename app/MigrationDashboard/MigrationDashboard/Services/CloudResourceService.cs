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

    public CloudResourceService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<CloudResourceDashboardViewModel> GetDashboardDataAsync()
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var bucketName = localStackConfig["MigrationBucketName"] ?? "cloud-migration-backup-kedep";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-kedep";
        var lambdaName = localStackConfig["SelfTestLambdaName"] ?? "cloud-migration-self-test-kedep";

        var viewModel = new CloudResourceDashboardViewModel
        {
            S3BucketName = bucketName,
            DynamoDbTableName = tableName,
            LambdaFunctionName = lambdaName
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
            viewModel.LambdaErrorMessage = $"Không kết nối hoặc định danh được Lambda Function '{lambdaName}': {ex.Message}";
        }

        // Populate simulated cost list
        viewModel.CostItems = new List<CostItemViewModel>
        {
            new() { ServiceName = "S3 Storage", Description = "Lưu trữ backup tệp tin JSON giả lập", CostPerMonth = viewModel.BackupFilesCount > 0 ? 0.08 : 0.0 },
            new() { ServiceName = "DynamoDB Requests", Description = "Ghi logs trạng thái tiến trình di trú", CostPerMonth = viewModel.MigrationLogsCount > 0 ? 0.10 : 0.0 },
            new() { ServiceName = "Lambda Invocations", Description = "Kích hoạt kiểm thử tự động sau di trú", CostPerMonth = hasPassedSelfTest ? 0.05 : 0.0 },
            new() { ServiceName = "Local Development/Docker", Description = "Môi trường giả lập LocalStack không phát sinh chi phí", CostPerMonth = 0.00 },
            new() { ServiceName = "Network/Other", Description = "Chi phí truyền dữ liệu và dịch vụ khác", CostPerMonth = (viewModel.BackupFilesCount > 0 || viewModel.MigrationLogsCount > 0) ? 0.07 : 0.0 }
        };

        viewModel.EstimatedMonthlyCost = viewModel.CostItems.Sum(c => c.CostPerMonth);

        // Populate before/after comparison list
        viewModel.ComparisonItems = new List<ComparisonItemViewModel>
        {
            new() { Category = "Hạ tầng", OnPremiseDetails = "Cấu hình thủ công trên máy local/server vật lý", CloudDetails = "Hạ tầng được mô tả bằng Terraform, có thể apply/destroy tự động", Icon = "bi-boxes" },
            new() { Category = "Lưu trữ file", OnPremiseDetails = "Lưu trên ổ đĩa máy chủ", CloudDetails = "Lưu trên S3 object storage", Icon = "bi-archive" },
            new() { Category = "Ghi log", OnPremiseDetails = "Log local hoặc SQL Server", CloudDetails = "Migration logs lưu trên DynamoDB", Icon = "bi-journal-text" },
            new() { Category = "Kiểm thử sau triển khai", OnPremiseDetails = "Kiểm tra thủ công", CloudDetails = "Lambda self-test tự động kiểm tra S3/DynamoDB", Icon = "bi-lightning-charge" },
            new() { Category = "Khả năng phục hồi", OnPremiseDetails = "Khó rollback, phụ thuộc backup thủ công", CloudDetails = "Có backup object, log trạng thái và rollback simulation", Icon = "bi-arrow-counterclockwise" },
            new() { Category = "Chi phí", OnPremiseDetails = "Khó ước tính chi tiết theo dịch vụ", CloudDetails = "Có bảng cost estimation theo từng thành phần", Icon = "bi-cash-stack" }
        };

        viewModel.MigrationHealthScore = healthScore;

        return viewModel;
    }

    private string GetAttributeString(Dictionary<string, AttributeValue> item, string key)
    {
        return item.TryGetValue(key, out var val) ? val.S : string.Empty;
    }
}
