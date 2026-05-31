using System;

namespace MigrationDashboard.Models;

public class S3ObjectViewModel
{
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}
