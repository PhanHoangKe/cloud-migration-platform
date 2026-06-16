using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class MigrationHistoryService : IMigrationHistoryService
{
    private readonly IConfiguration _configuration;

    public MigrationHistoryService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<MigrationHistoryViewModel> GetMigrationHistoryAsync()
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-vinhuni";

        var viewModel = new MigrationHistoryViewModel();

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region
        };

        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        try
        {
            var scanResponse = await dynamoClient.ScanAsync(new ScanRequest { TableName = tableName });
            var rawLogs = new List<MigrationLogItem>();

            if (scanResponse.Items != null && scanResponse.Items.Count > 0)
            {
                rawLogs = scanResponse.Items.Select(item => new MigrationLogItem
                {
                    MigrationId = item.TryGetValue("MigrationId", out var idVal) ? idVal.S : string.Empty,
                    Timestamp = item.TryGetValue("Timestamp", out var timeVal) ? timeVal.S : string.Empty,
                    Status = item.TryGetValue("Status", out var statusVal) ? statusVal.S : string.Empty,
                    Message = item.TryGetValue("Message", out var msgVal) ? msgVal.S : string.Empty
                }).ToList();
            }

            // Filter out empty migration IDs and group
            var groupedLogs = rawLogs
                .Where(l => !string.IsNullOrEmpty(l.MigrationId))
                .GroupBy(l => l.MigrationId)
                .ToList();

            foreach (var group in groupedLogs)
            {
                var migrationId = group.Key;
                
                // Parse timestamps and sort logs chronologically
                var sortedLogs = group
                    .Select(l => new
                    {
                        Log = l,
                        ParsedTime = DateTime.TryParse(l.Timestamp, out var dt) ? dt : DateTime.MinValue
                    })
                    .OrderBy(x => x.ParsedTime)
                    .ToList();

                if (sortedLogs.Count == 0) continue;

                var firstLog = sortedLogs.First();
                var latestLog = sortedLogs.Last();

                var startedAt = firstLog.ParsedTime == DateTime.MinValue ? (DateTime?)null : firstLog.ParsedTime;
                
                // Compute statuses
                var statuses = sortedLogs.Select(x => x.Log.Status).ToHashSet();
                
                bool isFailed = statuses.Contains("FAILED") || statuses.Contains("SELF_TEST_FAILED") || statuses.Contains("RESTORE_FAILED") || statuses.Contains("ROLLBACK_FAILED");
                bool isWarning = (statuses.Contains("DATABASE_EXPORT_FAILED") || statuses.Contains("DATABASE_EXPORT_WARNING")) && !isFailed;
                bool isCompleted = (statuses.Contains("COMPLETED") || statuses.Contains("SELF_TEST_PASSED") || statuses.Contains("REPORT_GENERATED") || statuses.Contains("REPORT_UPLOADED_TO_S3")) && !isFailed;

                // Determine current status label
                string currentStatus = "UNKNOWN";
                if (isFailed)
                {
                    currentStatus = "FAILED";
                }
                else if (isWarning)
                {
                    currentStatus = "WARNING";
                }
                else if (isCompleted)
                {
                    currentStatus = "COMPLETED";
                }
                else if (statuses.Contains("STARTED") || latestLog.Log.Status.Contains("STARTED"))
                {
                    currentStatus = "RUNNING";
                }

                // Completed At
                DateTime? completedAt = null;
                if (isFailed || isCompleted)
                {
                    completedAt = latestLog.ParsedTime == DateTime.MinValue ? (DateTime?)null : latestLog.ParsedTime;
                }

                // Duration Text
                string durationText = "N/A";
                if (startedAt.HasValue)
                {
                    var endTime = completedAt ?? DateTime.Now;
                    var duration = endTime - startedAt.Value;
                    if (duration.TotalSeconds < 0) duration = TimeSpan.Zero;

                    if (duration.TotalMinutes >= 1)
                    {
                        durationText = $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
                    }
                    else
                    {
                        durationText = $"{(int)duration.TotalSeconds}s";
                    }

                    if (!completedAt.HasValue && currentStatus == "RUNNING")
                    {
                        durationText += " (Đang chạy)";
                    }
                }

                // Completed milestones count
                int completedSteps = 0;
                if (statuses.Contains("STARTED")) completedSteps++;
                if (statuses.Contains("ASSESSMENT_EXPORTED")) completedSteps++;
                if (statuses.Contains("SOURCE_PACKAGED") || statuses.Contains("MANIFEST_CREATED")) completedSteps++;
                if (statuses.Contains("DATABASE_EXPORTED") || statuses.Contains("DATABASE_EXPORT_UPLOADED_TO_S3")) completedSteps++;
                if (statuses.Contains("UPLOADED_TO_S3")) completedSteps++;
                if (statuses.Contains("SELF_TEST_PASSED")) completedSteps++;
                if (statuses.Contains("REPORT_GENERATED") || statuses.Contains("REPORT_UPLOADED_TO_S3")) completedSteps++;

                // Warning & Error counts in logs
                int warningCount = sortedLogs.Count(x => 
                    x.Log.Status == "DATABASE_EXPORT_FAILED" || 
                    x.Log.Status == "DATABASE_EXPORT_WARNING" || 
                    x.Log.Status.Contains("WARNING")
                );

                int errorCount = sortedLogs.Count(x => 
                    (x.Log.Status.Contains("FAILED") && x.Log.Status != "DATABASE_EXPORT_FAILED") ||
                    x.Log.Status == "FAILED"
                );

                // Progress Percent
                int progressPercent = 0;
                // Identify the highest status percentage reached in the group
                var progressMapping = new Dictionary<string, int>
                {
                    { "STARTED", 10 },
                    { "ASSESSMENT_EXPORTED", 25 },
                    { "MANIFEST_CREATED", 40 },
                    { "SOURCE_PACKAGED", 40 },
                    { "DATABASE_CONNECTED", 55 },
                    { "DATABASE_EXPORTED", 55 },
                    { "DATABASE_EXPORT_UPLOADED_TO_S3", 55 },
                    { "DATABASE_EXPORT_FAILED", 55 },
                    { "UPLOADED_TO_S3", 70 },
                    { "SELF_TEST_TRIGGERED", 85 },
                    { "SELF_TEST_PASSED", 85 },
                    { "SELF_TEST_FAILED", 85 },
                    { "REPORT_GENERATED", 95 },
                    { "REPORT_UPLOADED_TO_S3", 95 },
                    { "COMPLETED", 100 }
                };

                foreach (var logItem in sortedLogs)
                {
                    if (progressMapping.TryGetValue(logItem.Log.Status, out int pct))
                    {
                        if (pct > progressPercent) progressPercent = pct;
                    }
                }

                // If progress percent could not be determined from mapping, fallback to completed steps count
                if (progressPercent == 0 && completedSteps > 0)
                {
                    progressPercent = (int)Math.Round((double)completedSteps / 7.0 * 100.0);
                }

                viewModel.Items.Add(new MigrationHistoryItemViewModel
                {
                    MigrationId = migrationId,
                    CurrentStatus = currentStatus,
                    CurrentStep = DetermineCurrentStep(latestLog.Log.Status),
                    ProgressPercent = progressPercent,
                    TotalLogs = sortedLogs.Count,
                    CompletedSteps = completedSteps,
                    WarningCount = warningCount,
                    ErrorCount = errorCount,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    DurationText = durationText
                });
            }

            // Sort history items so that the newest migrations appear first
            viewModel.Items = viewModel.Items
                .OrderByDescending(item => item.StartedAt ?? DateTime.MinValue)
                .ToList();

            // Set aggregate metrics
            viewModel.TotalMigrations = viewModel.Items.Count;
            viewModel.CompletedCount = viewModel.Items.Count(i => i.CurrentStatus == "COMPLETED");
            viewModel.FailedCount = viewModel.Items.Count(i => i.CurrentStatus == "FAILED");
            viewModel.WarningCount = viewModel.Items.Count(i => i.CurrentStatus == "WARNING");
        }
        catch (Exception ex)
        {
            viewModel.ErrorMessage = $"Lỗi truy vấn lịch sử di trú từ DynamoDB: {ex.Message}";
        }

        return viewModel;
    }

    private string DetermineCurrentStep(string status)
    {
        if (status.StartsWith("RESTORE_")) return "RESTORE_SIMULATION";
        if (status.StartsWith("ROLLBACK_")) return "ROLLBACK_SIMULATION";
        if (status.StartsWith("DATABASE_")) return "DATABASE_EXPORT";

        return status switch
        {
            "STARTED" => "MIGRATION_START",
            "ASSESSMENT_EXPORTED" => "PRE_ASSESSMENT",
            "MANIFEST_CREATED" => "MANIFEST_GENERATION",
            "SOURCE_PACKAGED" => "SOURCE_ZIP",
            "UPLOADED_TO_S3" => "S3_UPLOAD",
            "SELF_TEST_TRIGGERED" or "SELF_TEST_PASSED" or "SELF_TEST_FAILED" => "LAMBDA_SELF_TEST",
            "REPORT_GENERATED" or "REPORT_UPLOADED_TO_S3" => "MIGRATION_REPORT",
            "COMPLETED" => "COMPLETED",
            _ => "SYSTEM_LOG"
        };
    }
}
