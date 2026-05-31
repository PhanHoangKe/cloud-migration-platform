using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class DatabaseTableExportViewModel
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public List<DatabaseColumnViewModel> Columns { get; set; } = new();
    public List<Dictionary<string, object>> SampleRows { get; set; } = new();
}
