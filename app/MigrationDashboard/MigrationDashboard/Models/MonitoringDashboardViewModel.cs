using System;
using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class MonitoringTimelineItemViewModel
{
    public string MigrationId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusVietnamese { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
}

public class MonitoringStatusSummaryViewModel
{
    public int TotalLogs { get; set; }
    public int UniqueMigrationsCount { get; set; }
    public int SelfTestPassedCount { get; set; }
    public int RestoreCompletedCount { get; set; }
    public int RollbackCompletedCount { get; set; }
    public int FailedCount { get; set; }
}

public class MonitoringFilterViewModel
{
    public string? SelectedMigrationId { get; set; }
    public string? SelectedStatus { get; set; }
    public List<string> AvailableMigrationIds { get; set; } = new();
    public List<string> AvailableStatuses { get; set; } = new();
}

public class MonitoringDashboardViewModel
{
    public MonitoringStatusSummaryViewModel Summary { get; set; } = new();
    public List<MonitoringTimelineItemViewModel> TimelineItems { get; set; } = new();
    public MonitoringFilterViewModel Filter { get; set; } = new();
    public bool IsLocalStackAvailable { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, int> StatusChartData { get; set; } = new();
}
