using System;
using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class RestoreFileViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public bool ExistsOnS3 { get; set; }
    public bool DownloadedLocally { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public long Size { get; set; }
}

public class RestoreValidationItemViewModel
{
    public string CheckName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // PASSED, WARNING, FAILED
    public string Description { get; set; } = string.Empty;
}

public class RestoreResultViewModel
{
    public string RestoreId { get; set; } = string.Empty;
    public string MigrationId { get; set; } = string.Empty;
    public DateTime RestoredAt { get; set; }
    public string SourceBucket { get; set; } = string.Empty;
    public List<RestoreFileViewModel> RestoredFiles { get; set; } = new();
    public List<RestoreValidationItemViewModel> ValidationResults { get; set; } = new();
    public string RestoreStatus { get; set; } = string.Empty; // COMPLETED, WARNING, FAILED
    public string LocalRestorePath { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MigrationLogItem> Logs { get; set; } = new();
}
