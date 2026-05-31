using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class CloudResourceDashboardViewModel
{
    public string S3BucketName { get; set; } = string.Empty;
    public string DynamoDbTableName { get; set; } = string.Empty;
    public string LambdaFunctionName { get; set; } = string.Empty;

    public int BackupFilesCount { get; set; }
    public int MigrationLogsCount { get; set; }
    public string LatestSelfTestStatus { get; set; } = "N/A";
    public int MigrationHealthScore { get; set; }
    public double EstimatedMonthlyCost { get; set; }

    public bool IsLocalStackAvailable { get; set; } = true;
    public string? S3ErrorMessage { get; set; }
    public string? DynamoDbErrorMessage { get; set; }
    public string? LambdaErrorMessage { get; set; }

    public List<S3ObjectViewModel> RecentBackups { get; set; } = new();
    public List<MigrationLogItem> RecentLogs { get; set; } = new();
    public List<CostItemViewModel> CostItems { get; set; } = new();
    public List<ComparisonItemViewModel> ComparisonItems { get; set; } = new();
    public PreMigrationAssessmentViewModel Assessment { get; set; } = new();
}
