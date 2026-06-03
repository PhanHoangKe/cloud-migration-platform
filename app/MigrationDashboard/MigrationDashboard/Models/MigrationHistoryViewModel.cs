using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class MigrationHistoryViewModel
{
    public List<MigrationHistoryItemViewModel> Items { get; set; } = new();
    public int TotalMigrations { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int WarningCount { get; set; }
    public string? ErrorMessage { get; set; }
}
