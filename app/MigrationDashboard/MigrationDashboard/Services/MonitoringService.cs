using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class MonitoringService : IMonitoringService
{
    private readonly IConfiguration _configuration;

    public MonitoringService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<MonitoringDashboardViewModel> GetMonitoringDataAsync(string? migrationIdFilter, string? statusFilter)
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-kedep";

        var viewModel = new MonitoringDashboardViewModel();
        viewModel.Filter.SelectedMigrationId = migrationIdFilter;
        viewModel.Filter.SelectedStatus = statusFilter;

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region
        };
        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        try
        {
            var scanResponse = await dynamoClient.ScanAsync(new ScanRequest { TableName = tableName });
            viewModel.IsLocalStackAvailable = true;

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

            // A. Compute operational stats on ALL logs
            viewModel.Summary.TotalLogs = rawLogs.Count;
            viewModel.Summary.UniqueMigrationsCount = rawLogs
                .Select(l => l.MigrationId)
                .Where(id => id.StartsWith("MIGRATION_"))
                .Distinct()
                .Count();
            
            viewModel.Summary.SelfTestPassedCount = rawLogs.Count(l => l.Status == "SELF_TEST_PASSED");
            viewModel.Summary.RestoreCompletedCount = rawLogs.Count(l => l.Status == "RESTORE_COMPLETED");
            viewModel.Summary.RollbackCompletedCount = rawLogs.Count(l => l.Status == "ROLLBACK_COMPLETED");
            
            viewModel.Summary.FailedCount = rawLogs.Count(l => 
                l.Status == "FAILED" || 
                l.Status == "SELF_TEST_FAILED" || 
                l.Status == "RESTORE_FAILED" ||
                l.Status == "ROLLBACK_FAILED"
            );

            // B. Extract available filter values from ALL logs
            viewModel.Filter.AvailableMigrationIds = rawLogs
                .Select(l => l.MigrationId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .OrderByDescending(id => id)
                .ToList();

            viewModel.Filter.AvailableStatuses = rawLogs
                .Select(l => l.Status)
                .Where(st => !string.IsNullOrEmpty(st))
                .Distinct()
                .OrderBy(st => st)
                .ToList();

            // C. Count logs by Status for Chart.js (ALL logs to give an overall health metric)
            var chartStatuses = new[] {
                "STARTED", "UPLOADED_TO_S3", "SELF_TEST_PASSED", "REPORT_GENERATED", 
                "RESTORE_COMPLETED", "ROLLBACK_COMPLETED", "FAILED"
            };

            foreach (var status in chartStatuses)
            {
                int count;
                if (status == "FAILED")
                {
                    count = rawLogs.Count(l => 
                        l.Status == "FAILED" || 
                        l.Status == "SELF_TEST_FAILED" || 
                        l.Status == "RESTORE_FAILED"
                    );
                }
                else
                {
                    count = rawLogs.Count(l => l.Status == status);
                }
                viewModel.StatusChartData[status] = count;
            }

            // D. Filter logs for table & timeline display
            var filteredLogs = rawLogs.AsEnumerable();
            if (!string.IsNullOrEmpty(migrationIdFilter))
            {
                filteredLogs = filteredLogs.Where(l => l.MigrationId == migrationIdFilter);
            }
            if (!string.IsNullOrEmpty(statusFilter))
            {
                filteredLogs = filteredLogs.Where(l => l.Status == statusFilter);
            }

            // E. Map to TimelineItemViewModels ordered by newest first
            viewModel.TimelineItems = filteredLogs
                .OrderByDescending(l => l.Timestamp)
                .Select(l =>
                {
                    var mapped = MapStatus(l.Status);
                    var colors = GetStatusStyles(l.Status);

                    return new MonitoringTimelineItemViewModel
                    {
                        MigrationId = l.MigrationId,
                        Timestamp = FormatTimestamp(l.Timestamp),
                        Status = l.Status,
                        StatusVietnamese = mapped,
                        StatusBadgeClass = colors.BadgeClass,
                        IconClass = colors.IconClass,
                        Step = DetermineStep(l.Status),
                        Message = l.Message
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            viewModel.IsLocalStackAvailable = false;
            viewModel.ErrorMessage = $"Lỗi kết nối cơ sở dữ liệu DynamoDB '{tableName}' của LocalStack: {ex.Message}";
        }

        return viewModel;
    }

    private string MapStatus(string status)
    {
        return status switch
        {
            "STARTED" => "Đã bắt đầu",
            "ASSESSMENT_EXPORTED" => "Đã xuất đánh giá",
            "MANIFEST_CREATED" => "Đã tạo manifest",
            "SOURCE_PACKAGED" => "Đã đóng gói source",
            "UPLOADED_TO_S3" => "Đã tải lên S3",
            "SELF_TEST_TRIGGERED" => "Đã gọi tự kiểm thử",
            "SELF_TEST_PASSED" => "Tự kiểm thử đạt",
            "SELF_TEST_FAILED" => "Tự kiểm thử lỗi",
            "REPORT_GENERATED" => "Đã tạo báo cáo",
            "REPORT_UPLOADED_TO_S3" => "Đã tải báo cáo lên S3",
            "ROLLBACK_STARTED" => "Bắt đầu rollback",
            "ROLLBACK_COMPLETED" => "Rollback hoàn tất",
            "ROLLBACK_FAILED" => "Rollback thất bại",
            "RESTORE_STARTED" => "Bắt đầu khôi phục",
            "RESTORE_PACKAGE_VALIDATED" => "Đã kiểm tra gói khôi phục",
            "RESTORE_FILES_DOWNLOADED" => "Đã tải file khôi phục",
            "RESTORE_COMPLETED" => "Khôi phục hoàn tất",
            "RESTORE_FAILED" => "Khôi phục thất bại",
            "FAILED" => "Lỗi di trú",
            _ => status
        };
    }

    private (string BadgeClass, string IconClass) GetStatusStyles(string status)
    {
        return status switch
        {
            "SELF_TEST_PASSED" or "RESTORE_COMPLETED" or "ROLLBACK_COMPLETED" or "REPORT_UPLOADED_TO_S3" or "UPLOADED_TO_S3" 
                => ("success", "bi-check-circle-fill text-success"),

            "SELF_TEST_TRIGGERED" or "RESTORE_PACKAGE_VALIDATED" or "STARTED" or "RESTORE_STARTED" or "ROLLBACK_STARTED"
                => ("warning text-dark", "bi-play-circle-fill text-warning"),

            "FAILED" or "SELF_TEST_FAILED" or "RESTORE_FAILED" or "ROLLBACK_FAILED"
                => ("danger", "bi-x-circle-fill text-danger"),

            "REPORT_GENERATED" or "ASSESSMENT_EXPORTED" or "MANIFEST_CREATED" or "SOURCE_PACKAGED" or "RESTORE_FILES_DOWNLOADED"
                => ("info", "bi-info-circle-fill text-info"),

            _ => ("secondary", "bi-arrow-right-circle text-secondary")
        };
    }

    private string DetermineStep(string status)
    {
        if (status.StartsWith("RESTORE_")) return "RESTORE_SIMULATION";
        if (status.StartsWith("ROLLBACK_")) return "ROLLBACK_SIMULATION";
        
        return status switch
        {
            "STARTED" => "MIGRATION_START",
            "ASSESSMENT_EXPORTED" => "PRE_ASSESSMENT",
            "MANIFEST_CREATED" => "MANIFEST_GENERATION",
            "SOURCE_PACKAGED" => "SOURCE_ZIP",
            "UPLOADED_TO_S3" => "S3_UPLOAD",
            "SELF_TEST_TRIGGERED" or "SELF_TEST_PASSED" or "SELF_TEST_FAILED" => "LAMBDA_SELF_TEST",
            "REPORT_GENERATED" or "REPORT_UPLOADED_TO_S3" => "MIGRATION_REPORT",
            _ => "SYSTEM_LOG"
        };
    }

    private string FormatTimestamp(string timestamp)
    {
        if (DateTime.TryParse(timestamp, out var dt))
        {
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        return timestamp;
    }
}
