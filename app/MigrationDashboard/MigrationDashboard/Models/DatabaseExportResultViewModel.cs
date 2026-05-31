using System;
using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class DatabaseExportResultViewModel
{
    public string MigrationId { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; }
    public string DatabaseType { get; set; } = "SQL Server";
    public bool ConnectionStatus { get; set; }
    public int TotalTables { get; set; }
    public int TotalRows { get; set; }
    public List<DatabaseTableExportViewModel> Tables { get; set; } = new();
    public string AcademicNote { get; set; } = "This database export is generated for academic migration simulation. Only a limited number of sample rows are exported.";
    public string? ErrorMessage { get; set; }
    public string? S3Key { get; set; }
}
