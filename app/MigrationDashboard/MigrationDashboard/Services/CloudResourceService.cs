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

                // Check for SELF_TEST_PASSED log status
                if (logs.Any(l => l.Status == "SELF_TEST_PASSED"))
                {
                    hasPassedSelfTest = true;
                    healthScore += 25;
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

        // Compute simulated cost based on resources
        double cost = 0.0;
        if (viewModel.BackupFilesCount > 0) cost += 0.08;
        if (viewModel.MigrationLogsCount > 0) cost += 0.10;
        if (hasPassedSelfTest) cost += 0.05;
        if (cost > 0) cost += 0.07; // Other/static simulated charges
        viewModel.EstimatedMonthlyCost = cost;

        viewModel.MigrationHealthScore = healthScore;

        return viewModel;
    }

    private string GetAttributeString(Dictionary<string, AttributeValue> item, string key)
    {
        return item.TryGetValue(key, out var val) ? val.S : string.Empty;
    }
}
