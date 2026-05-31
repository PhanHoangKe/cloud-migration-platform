using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class MigrationResultViewModel
{
    public string MigrationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BackupFileName { get; set; } = string.Empty;
    public string S3BucketName { get; set; } = string.Empty;
    public string S3ObjectKey { get; set; } = string.Empty;
    public List<string> S3ObjectKeys { get; set; } = new();
    public string PackageSize { get; set; } = string.Empty;
    public int ReadinessScore { get; set; }
    public string LambdaSelfTestResponse { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MigrationLogItem> Logs { get; set; } = new();
    
    // New Report fields
    public string ReportJsonS3Key { get; set; } = string.Empty;
    public string ReportHtmlS3Key { get; set; } = string.Empty;
    public bool ReportGenerated { get; set; }
    public string ReportLocalHtmlPath { get; set; } = string.Empty;

    // Database Export fields
    public string DatabaseExportStatus { get; set; } = string.Empty;
    public string DatabaseExportS3Key { get; set; } = string.Empty;
    public int DatabaseTableCount { get; set; }
    public int DatabaseTotalRows { get; set; }
    public string? DatabaseExportErrorMessage { get; set; }
}
