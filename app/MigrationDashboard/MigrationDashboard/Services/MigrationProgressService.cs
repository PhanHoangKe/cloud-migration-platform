using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class MigrationProgressService : IMigrationProgressService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private static readonly ConcurrentDictionary<string, int> _maxProgress = new();

    public MigrationProgressService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _env = env;
    }

    public async Task<MigrationProgressViewModel?> GetProgressAsync(string migrationId)
    {
        // 1. Check local packages directory to see if migration ID exists at all
        var tempDirectory = Path.Combine(_env.WebRootPath, "migration-packages", migrationId);
        bool directoryExists = Directory.Exists(tempDirectory);

        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-vinhuni";

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region,
            Timeout = TimeSpan.FromSeconds(3)
        };

        var responseLogs = new List<MigrationLogItem>();
        bool dbError = false;

        try
        {
            using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);
            var queryRequest = new QueryRequest
            {
                TableName = tableName,
                KeyConditionExpression = "MigrationId = :mId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":mId", new AttributeValue { S = migrationId } }
                },
                ConsistentRead = true
            };

            var queryResponse = await dynamoClient.QueryAsync(queryRequest);
            if (queryResponse.Items != null && queryResponse.Items.Count > 0)
            {
                responseLogs = queryResponse.Items.Select(item => new MigrationLogItem
                {
                    MigrationId = item.TryGetValue("MigrationId", out var idVal) ? idVal.S : string.Empty,
                    Timestamp = item.TryGetValue("Timestamp", out var timeVal) ? timeVal.S : string.Empty,
                    Status = item.TryGetValue("Status", out var statusVal) ? statusVal.S : string.Empty,
                    Message = item.TryGetValue("Message", out var msgVal) ? msgVal.S : string.Empty
                })
                .OrderBy(l => l.Timestamp)
                .ToList();
            }
        }
        catch (Exception)
        {
            dbError = true;
        }

        // If not found in DynamoDB AND local directory does not exist, return null
        if (responseLogs.Count == 0 && !directoryExists)
        {
            return null;
        }

        // Initialize steps
        var steps = new List<MigrationStepProgressViewModel>
        {
            new() { StepName = "PREPARING", DisplayName = "Chuẩn bị chuyển đổi", TargetPercent = 10 },
            new() { StepName = "ASSESSMENT", DisplayName = "Đánh giá trước chuyển đổi", TargetPercent = 25 },
            new() { StepName = "PACKAGING", DisplayName = "Đóng gói mã nguồn", TargetPercent = 40 },
            new() { StepName = "DATABASE_EXPORT", DisplayName = "Xuất dữ liệu SQL Server", TargetPercent = 55 },
            new() { StepName = "UPLOAD_TO_S3", DisplayName = "Tải gói lên S3", TargetPercent = 70 },
            new() { StepName = "SELF_TEST", DisplayName = "Kiểm thử tự động Lambda", TargetPercent = 85 },
            new() { StepName = "REPORT", DisplayName = "Khởi tạo báo cáo di trú", TargetPercent = 95 },
            new() { StepName = "COMPLETED", DisplayName = "Hoàn tất chuyển đổi", TargetPercent = 100 }
        };

        var logMap = responseLogs.ToDictionary(l => l.Status, l => l.Timestamp);

        // Helper functions
        bool HasLog(string status) => logMap.ContainsKey(status);
        string? GetTime(string status)
        {
            if (logMap.TryGetValue(status, out var t))
            {
                try
                {
                    if (DateTime.TryParse(t, out var dt))
                    {
                        return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
                catch { }
                return t;
            }
            return null;
        }

        // 1. PREPARING
        if (HasLog("STARTED"))
        {
            steps[0].Status = "COMPLETED";
            steps[0].CompletedAt = GetTime("STARTED");
        }

        // 2. ASSESSMENT
        if (HasLog("ASSESSMENT_EXPORTED"))
        {
            steps[1].Status = "COMPLETED";
            steps[1].CompletedAt = GetTime("ASSESSMENT_EXPORTED");
        }

        // 3. PACKAGING
        if (HasLog("SOURCE_PACKAGED"))
        {
            steps[2].Status = "COMPLETED";
            steps[2].CompletedAt = GetTime("SOURCE_PACKAGED");
        }

        // 4. DATABASE_EXPORT
        if (HasLog("DATABASE_EXPORTED") || HasLog("DATABASE_EXPORT_UPLOADED_TO_S3"))
        {
            steps[3].Status = "COMPLETED";
            steps[3].CompletedAt = GetTime("DATABASE_EXPORTED") ?? GetTime("DATABASE_EXPORT_UPLOADED_TO_S3");
        }
        else if (HasLog("DATABASE_EXPORT_FAILED"))
        {
            steps[3].Status = "WARNING";
            steps[3].CompletedAt = GetTime("DATABASE_EXPORT_FAILED");
        }

        // 5. UPLOAD_TO_S3
        if (HasLog("UPLOADED_TO_S3"))
        {
            steps[4].Status = "COMPLETED";
            steps[4].CompletedAt = GetTime("UPLOADED_TO_S3");
        }

        // 6. SELF_TEST
        if (HasLog("SELF_TEST_PASSED"))
        {
            steps[5].Status = "COMPLETED";
            steps[5].CompletedAt = GetTime("SELF_TEST_PASSED");
        }
        else if (HasLog("SELF_TEST_FAILED"))
        {
            steps[5].Status = "FAILED";
            steps[5].CompletedAt = GetTime("SELF_TEST_FAILED");
        }

        // 7. REPORT
        if (HasLog("REPORT_GENERATED") || HasLog("REPORT_UPLOADED_TO_S3"))
        {
            steps[6].Status = "COMPLETED";
            steps[6].CompletedAt = GetTime("REPORT_GENERATED") ?? GetTime("REPORT_UPLOADED_TO_S3");
        }
        else if (HasLog("REPORT_FAILED"))
        {
            steps[6].Status = "FAILED";
            steps[6].CompletedAt = GetTime("REPORT_FAILED");
        }

        // 8. COMPLETED
        if (HasLog("COMPLETED"))
        {
            steps[7].Status = "COMPLETED";
            steps[7].CompletedAt = GetTime("COMPLETED");
        }

        // Back-propagate completion status: if a later step is completed/warning/failed, prior steps cannot be pending
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Status == "COMPLETED" || steps[i].Status == "WARNING" || steps[i].Status == "FAILED")
            {
                for (int j = 0; j < i; j++)
                {
                    if (steps[j].Status == "PENDING")
                    {
                        steps[j].Status = "COMPLETED";
                    }
                }
            }
        }

        int progressPercent = 0;
        string currentStatus = "PENDING";

        if (dbError && responseLogs.Count == 0)
        {
            currentStatus = "ERROR";
            progressPercent = 0;
        }
        else
        {
            var firstUnfinishedIndex = steps.FindIndex(s => s.Status == "PENDING");
            if (firstUnfinishedIndex == -1)
            {
                progressPercent = 100;
                currentStatus = steps.Any(s => s.Status == "FAILED") ? "FAILED" : "COMPLETED";
            }
            else
            {
                bool hasGeneralFailed = responseLogs.Any(l => l.Status == "FAILED");
                if (hasGeneralFailed)
                {
                    steps[firstUnfinishedIndex].Status = "FAILED";
                    currentStatus = "FAILED";
                }
                else
                {
                    steps[firstUnfinishedIndex].Status = "RUNNING";
                    currentStatus = steps[firstUnfinishedIndex].StepName;
                }

                if (firstUnfinishedIndex > 0)
                {
                    progressPercent = steps[firstUnfinishedIndex - 1].TargetPercent;
                }
                else
                {
                    progressPercent = 5;
                }
            }
        }

        // Ensure progress is monotonic (never goes backwards)
        progressPercent = _maxProgress.AddOrUpdate(migrationId, progressPercent, (key, oldVal) => Math.Max(oldVal, progressPercent));

        return new MigrationProgressViewModel
        {
            MigrationId = migrationId,
            CurrentStatus = currentStatus,
            ProgressPercent = progressPercent,
            Steps = steps
        };
    }
}
