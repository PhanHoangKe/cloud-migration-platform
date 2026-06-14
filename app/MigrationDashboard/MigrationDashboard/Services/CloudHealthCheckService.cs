using System;
using System.Linq;
using System.Net.Http;
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

public class CloudHealthCheckService : ICloudHealthCheckService
{
    private readonly IConfiguration _configuration;

    public CloudHealthCheckService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<CloudHealthCheckViewModel> GetCloudHealthCheckAsync()
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = DisasterState.CurrentRegion;
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        
        var bucketName = localStackConfig["MigrationBucketName"] ?? "cloud-migration-backup-kedep";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-kedep";
        var lambdaName = localStackConfig["SelfTestLambdaName"] ?? "cloud-migration-self-test-kedep";

        if (region == "ap-northeast-1")
        {
            bucketName = "cloud-migration-backup-kedep-tokyo";
            tableName = "cloud-migration-logs-kedep-tokyo";
            lambdaName = "cloud-migration-self-test-kedep-tokyo";
        }

        var viewModel = new CloudHealthCheckViewModel
        {
            CheckedAt = DateTime.Now
        };

        // Initialize health check items
        var itemGateway = new CloudHealthCheckItemViewModel
        {
            Name = "LocalStack Gateway",
            Description = "Mock Cloud API Gateway run locally via Docker.",
            IsRequired = true,
            Status = "Unknown",
            Recommendation = "Start Docker Desktop and run docker compose up -d"
        };

        var itemBucket = new CloudHealthCheckItemViewModel
        {
            Name = "S3 Backup Bucket",
            Description = $"Storage bucket for migration files. ({bucketName})",
            IsRequired = true,
            Status = "Unknown",
            Recommendation = "Run terraform apply -auto-approve"
        };

        var itemTable = new CloudHealthCheckItemViewModel
        {
            Name = "DynamoDB Logs Table",
            Description = $"Logs storage table. ({tableName})",
            IsRequired = true,
            Status = "Unknown",
            Recommendation = "Run terraform apply -auto-approve"
        };

        var itemLambda = new CloudHealthCheckItemViewModel
        {
            Name = "Lambda Self-test Function",
            Description = $"Serverless function for post-migration checks. ({lambdaName})",
            IsRequired = false,
            Status = "Unknown",
            Recommendation = "Run terraform apply -auto-approve"
        };

        var itemS3Perm = new CloudHealthCheckItemViewModel
        {
            Name = "S3 List Permission",
            Description = "Verifies listing privilege for backups.",
            IsRequired = false,
            Status = "Unknown",
            Recommendation = "Check credentials configuration or AWS CLI configuration"
        };

        var itemDynamoPerm = new CloudHealthCheckItemViewModel
        {
            Name = "DynamoDB Read Permission",
            Description = "Verifies scan/read operations on logs.",
            IsRequired = false,
            Status = "Unknown",
            Recommendation = "Check credentials configuration or DynamoDB table schema"
        };

        if (DisasterState.IsSingaporeDown && region == "ap-southeast-1")
        {
            itemGateway.Status = "Critical";
            itemGateway.StatusText = "Singapore Down";
            itemGateway.Details = "CRITICAL: Region Singapore (ap-southeast-1) is DOWN! App is offline!";
            itemGateway.Recommendation = "Đi đến Phòng điều hành khẩn cấp (Disaster Recovery Command Center) để kích hoạt Failover sang Tokyo.";

            string failMsg = "Vùng Singapore (ap-southeast-1) đã ngừng hoạt động.";
            itemBucket.Status = "Critical"; itemBucket.StatusText = "Offline"; itemBucket.Details = failMsg;
            itemTable.Status = "Critical"; itemTable.StatusText = "Offline"; itemTable.Details = failMsg;
            itemLambda.Status = "Critical"; itemLambda.StatusText = "Offline"; itemLambda.Details = failMsg;
            itemS3Perm.Status = "Critical"; itemS3Perm.StatusText = "Offline"; itemS3Perm.Details = failMsg;
            itemDynamoPerm.Status = "Critical"; itemDynamoPerm.StatusText = "Offline"; itemDynamoPerm.Details = failMsg;

            viewModel.Items.Add(itemGateway);
            viewModel.Items.Add(itemBucket);
            viewModel.Items.Add(itemTable);
            viewModel.Items.Add(itemLambda);
            viewModel.Items.Add(itemS3Perm);
            viewModel.Items.Add(itemDynamoPerm);

            viewModel.HealthScore = 0;
            viewModel.OverallStatus = "Critical";
            return viewModel;
        }

        // Step 1: Check LocalStack Gateway Ping
        bool isGatewayConnected = false;
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await httpClient.GetAsync(serviceUrl);
            isGatewayConnected = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.InternalServerError;
            
            if (isGatewayConnected)
            {
                itemGateway.Status = "Healthy";
                itemGateway.StatusText = "Respond OK";
                itemGateway.Details = $"Connected to gateway on service endpoint: {serviceUrl}";
                itemGateway.Recommendation = string.Empty;
            }
            else
            {
                itemGateway.Status = "Critical";
                itemGateway.StatusText = "Unreachable";
                itemGateway.Details = $"Gateway responded with unexpected code: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            itemGateway.Status = "Critical";
            itemGateway.StatusText = "Offline";
            itemGateway.Details = $"Connection failed: {ex.Message}";
        }

        // If Gateway is offline, propagate failure to other services unless we are in Tokyo under DR failover
        if (itemGateway.Status == "Critical")
        {
            if (DisasterState.IsFailedOver && region == "ap-northeast-1")
            {
                // Override gateway to mock healthy for DR demo
                itemGateway.Status = "Healthy";
                itemGateway.StatusText = "Active (DR Mock)";
                itemGateway.Details = "LocalStack Gateway is simulated active for Tokyo Region failover demo.";
                itemGateway.Recommendation = string.Empty;
                
                // Set the rest to Healthy
                itemBucket.Status = "Healthy"; itemBucket.StatusText = "Active (DR Mock)"; itemBucket.Details = $"Bucket '{bucketName}' hoạt động bình thường trong chế độ Phục hồi Thảm họa."; itemBucket.Recommendation = string.Empty;
                itemTable.Status = "Healthy"; itemTable.StatusText = "Active (DR Mock)"; itemTable.Details = $"Bảng logs '{tableName}' đã sẵn sàng nhận dữ liệu tại vùng Tokyo."; itemTable.Recommendation = string.Empty;
                itemLambda.Status = "Healthy"; itemLambda.StatusText = "Active (DR Mock)"; itemLambda.Details = $"Lambda kiểm thử tự động '{lambdaName}' sẵn sàng kích hoạt."; itemLambda.Recommendation = string.Empty;
                itemS3Perm.Status = "Healthy"; itemS3Perm.StatusText = "Granted (DR Mock)"; itemS3Perm.Details = "Quyền truy cập danh sách bucket đã được phê duyệt cho vùng Tokyo."; itemS3Perm.Recommendation = string.Empty;
                itemDynamoPerm.Status = "Healthy"; itemDynamoPerm.StatusText = "Granted (DR Mock)"; itemDynamoPerm.Details = "Quyền đọc/ghi logs hoạt động tốt trên vùng Tokyo."; itemDynamoPerm.Recommendation = string.Empty;
            }
            else
            {
                string failMsg = "Cannot verify because LocalStack Gateway is offline.";
                itemBucket.Status = "Critical"; itemBucket.StatusText = "Offline"; itemBucket.Details = failMsg;
                itemTable.Status = "Critical"; itemTable.StatusText = "Offline"; itemTable.Details = failMsg;
                itemLambda.Status = "Critical"; itemLambda.StatusText = "Offline"; itemLambda.Details = failMsg;
                itemS3Perm.Status = "Critical"; itemS3Perm.StatusText = "Offline"; itemS3Perm.Details = failMsg;
                itemDynamoPerm.Status = "Critical"; itemDynamoPerm.StatusText = "Offline"; itemDynamoPerm.Details = failMsg;
            }
        }
        else
        {
            // Set up AWS configurations
            var s3Config = new AmazonS3Config { ServiceURL = serviceUrl, ForcePathStyle = true, AuthenticationRegion = region, Timeout = TimeSpan.FromSeconds(2) };
            using var s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);

            var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = serviceUrl, AuthenticationRegion = region, Timeout = TimeSpan.FromSeconds(2) };
            using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

            var lambdaConfig = new AmazonLambdaConfig { ServiceURL = serviceUrl, AuthenticationRegion = region, Timeout = TimeSpan.FromSeconds(2) };
            using var lambdaClient = new AmazonLambdaClient(accessKey, secretKey, lambdaConfig);

            // Step 2 & 3: Check S3 Bucket & Permission
            try
            {
                var s3Response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucketName, MaxKeys = 1 });
                itemBucket.Status = "Healthy";
                itemBucket.StatusText = "Active";
                itemBucket.Details = $"Bucket '{bucketName}' exists and is ready.";
                itemBucket.Recommendation = string.Empty;

                itemS3Perm.Status = "Healthy";
                itemS3Perm.StatusText = "Granted";
                itemS3Perm.Details = $"List privilege OK. Listed {s3Response.KeyCount} object(s).";
                itemS3Perm.Recommendation = string.Empty;
            }
            catch (Exception ex)
            {
                if (DisasterState.IsFailedOver && region == "ap-northeast-1")
                {
                    itemBucket.Status = "Healthy";
                    itemBucket.StatusText = "Active (DR Mock)";
                    itemBucket.Details = $"Bucket '{bucketName}' hoạt động bình thường trong chế độ Phục hồi Thảm họa.";
                    itemBucket.Recommendation = string.Empty;

                    itemS3Perm.Status = "Healthy";
                    itemS3Perm.StatusText = "Granted (DR Mock)";
                    itemS3Perm.Details = "Quyền truy cập danh sách bucket đã được phê duyệt cho vùng Tokyo.";
                    itemS3Perm.Recommendation = string.Empty;
                }
                else
                {
                    itemBucket.Status = (ex is AmazonS3Exception s3Ex && (s3Ex.StatusCode == System.Net.HttpStatusCode.NotFound || s3Ex.ErrorCode == "NoSuchBucket")) ? "Critical" : "Warning";
                    itemBucket.StatusText = (ex is AmazonS3Exception s3Ex2 && (s3Ex2.StatusCode == System.Net.HttpStatusCode.NotFound || s3Ex2.ErrorCode == "NoSuchBucket")) ? "Missing" : "Restricted";
                    itemBucket.Details = (ex is AmazonS3Exception s3Ex3 && (s3Ex3.StatusCode == System.Net.HttpStatusCode.NotFound || s3Ex3.ErrorCode == "NoSuchBucket")) 
                        ? $"Bucket '{bucketName}' does not exist on LocalStack." 
                        : $"S3 response failed: {ex.Message}";

                    itemS3Perm.Status = itemBucket.Status;
                    itemS3Perm.StatusText = itemBucket.Status == "Critical" ? "Skipped" : "Restricted";
                    itemS3Perm.Details = itemBucket.Status == "Critical" 
                        ? "Bucket does not exist, skipping permission check." 
                        : $"List operation failed: {ex.Message}";
                }
            }

            // Step 4: Check DynamoDB Logs Table Existence
            bool tableExists = false;
            try
            {
                var describeResponse = await dynamoClient.DescribeTableAsync(new DescribeTableRequest { TableName = tableName });
                var tableStatus = describeResponse.Table?.TableStatus;
                
                if (tableStatus == TableStatus.ACTIVE)
                {
                    tableExists = true;
                    itemTable.Status = "Healthy";
                    itemTable.StatusText = "Active";
                    itemTable.Details = $"Table '{tableName}' is active (Status: {tableStatus}).";
                    itemTable.Recommendation = string.Empty;
                }
                else
                {
                    itemTable.Status = "Warning";
                    itemTable.StatusText = "Pending";
                    itemTable.Details = $"Table exists but status is: {tableStatus}.";
                }
            }
            catch (Exception ex)
            {
                if (DisasterState.IsFailedOver && region == "ap-northeast-1")
                {
                    tableExists = true;
                    itemTable.Status = "Healthy";
                    itemTable.StatusText = "Active (DR Mock)";
                    itemTable.Details = $"Bảng logs '{tableName}' đã sẵn sàng nhận dữ liệu tại vùng Tokyo.";
                    itemTable.Recommendation = string.Empty;
                }
                else
                {
                    itemTable.Status = "Critical";
                    itemTable.StatusText = (ex is Amazon.DynamoDBv2.Model.ResourceNotFoundException) ? "Missing" : "Error";
                    itemTable.Details = (ex is Amazon.DynamoDBv2.Model.ResourceNotFoundException) 
                        ? $"Table '{tableName}' does not exist on LocalStack." 
                        : $"Verification error: {ex.Message}";
                }
            }

            // Step 5: Check DynamoDB Read Permission
            if (tableExists)
            {
                try
                {
                    if (DisasterState.IsFailedOver && region == "ap-northeast-1" && itemTable.StatusText == "Active (DR Mock)")
                    {
                        itemDynamoPerm.Status = "Healthy";
                        itemDynamoPerm.StatusText = "Granted (DR Mock)";
                        itemDynamoPerm.Details = "Quyền đọc/ghi logs hoạt động tốt trên vùng Tokyo.";
                        itemDynamoPerm.Recommendation = string.Empty;
                    }
                    else
                    {
                        var scanResponse = await dynamoClient.ScanAsync(new ScanRequest { TableName = tableName, Limit = 1 });
                        itemDynamoPerm.Status = "Healthy";
                        itemDynamoPerm.StatusText = "Granted";
                        itemDynamoPerm.Details = "Scan/Read operation completed successfully.";
                        itemDynamoPerm.Recommendation = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    if (DisasterState.IsFailedOver && region == "ap-northeast-1")
                    {
                        itemDynamoPerm.Status = "Healthy";
                        itemDynamoPerm.StatusText = "Granted (DR Mock)";
                        itemDynamoPerm.Details = "Quyền đọc/ghi logs hoạt động tốt trên vùng Tokyo.";
                        itemDynamoPerm.Recommendation = string.Empty;
                    }
                    else
                    {
                        itemDynamoPerm.Status = "Warning";
                        itemDynamoPerm.StatusText = "Restricted";
                        itemDynamoPerm.Details = $"Scan operation failed: {ex.Message}";
                    }
                }
            }
            else
            {
                itemDynamoPerm.Status = "Critical";
                itemDynamoPerm.StatusText = "Skipped";
                itemDynamoPerm.Details = "Table does not exist, skipping permission check.";
            }

            // Step 6: Check Lambda Function Existence
            try
            {
                var lambdaResponse = await lambdaClient.GetFunctionAsync(new GetFunctionRequest { FunctionName = lambdaName });
                itemLambda.Status = "Healthy";
                itemLambda.StatusText = "Active";
                itemLambda.Details = $"Lambda '{lambdaName}' is active (Runtime: {lambdaResponse.Configuration?.Runtime}).";
                itemLambda.Recommendation = string.Empty;
            }
            catch (Exception ex)
            {
                if (DisasterState.IsFailedOver && region == "ap-northeast-1")
                {
                    itemLambda.Status = "Healthy";
                    itemLambda.StatusText = "Active (DR Mock)";
                    itemLambda.Details = $"Lambda kiểm thử tự động '{lambdaName}' sẵn sàng kích hoạt.";
                    itemLambda.Recommendation = string.Empty;
                }
                else
                {
                    itemLambda.Status = "Warning";
                    itemLambda.StatusText = (ex is Amazon.Lambda.Model.ResourceNotFoundException) ? "Missing" : "Error";
                    itemLambda.Details = (ex is Amazon.Lambda.Model.ResourceNotFoundException) 
                        ? $"Lambda function '{lambdaName}' is not deployed." 
                        : $"Verification error: {ex.Message}";
                }
            }
        }

        // Add checked items to list
        viewModel.Items.Add(itemGateway);
        viewModel.Items.Add(itemBucket);
        viewModel.Items.Add(itemTable);
        viewModel.Items.Add(itemLambda);
        viewModel.Items.Add(itemS3Perm);
        viewModel.Items.Add(itemDynamoPerm);

        // Step 7: Calculate HealthScore
        // Weights: Gateway: 25, Bucket: 20, Table: 20, Lambda: 15, S3Perm: 10, DynamoPerm: 10
        int score = 0;
        
        // Gateway (25 pts)
        if (itemGateway.Status == "Healthy") score += 25;
        else if (itemGateway.Status == "Warning") score += 10;

        // Bucket (20 pts)
        if (itemBucket.Status == "Healthy") score += 20;
        else if (itemBucket.Status == "Warning") score += 8;

        // Table (20 pts)
        if (itemTable.Status == "Healthy") score += 20;
        else if (itemTable.Status == "Warning") score += 8;

        // Lambda (15 pts)
        if (itemLambda.Status == "Healthy") score += 15;
        else if (itemLambda.Status == "Warning") score += 5;

        // S3 list perm (10 pts)
        if (itemS3Perm.Status == "Healthy") score += 10;
        else if (itemS3Perm.Status == "Warning") score += 4;

        // DynamoDb read perm (10 pts)
        if (itemDynamoPerm.Status == "Healthy") score += 10;
        else if (itemDynamoPerm.Status == "Warning") score += 4;

        viewModel.HealthScore = score;

        // Overall status
        if (score >= 85)
        {
            viewModel.OverallStatus = "Healthy";
        }
        else if (score >= 60)
        {
            viewModel.OverallStatus = "Warning";
        }
        else
        {
            viewModel.OverallStatus = "Critical";
        }

        return viewModel;
    }
}
