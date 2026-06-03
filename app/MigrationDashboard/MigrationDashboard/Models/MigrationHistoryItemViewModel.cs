using System;

namespace MigrationDashboard.Models;

public class MigrationHistoryItemViewModel
{
    public string MigrationId { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public int TotalLogs { get; set; }
    public int CompletedSteps { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string DurationText { get; set; } = string.Empty;
}
