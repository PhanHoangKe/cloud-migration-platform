using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class MigrationResultViewModel
{
    public string MigrationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BackupFileName { get; set; } = string.Empty;
    public string S3BucketName { get; set; } = string.Empty;
    public string S3ObjectKey { get; set; } = string.Empty;
    public string LambdaSelfTestResponse { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MigrationLogItem> Logs { get; set; } = new();
}
