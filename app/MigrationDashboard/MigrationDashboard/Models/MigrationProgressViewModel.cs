using System;
using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class MigrationProgressViewModel
{
    public string MigrationId { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public List<MigrationStepProgressViewModel> Steps { get; set; } = new();
}

public class MigrationStepProgressViewModel
{
    public string StepName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING, RUNNING, COMPLETED, FAILED, WARNING
    public int TargetPercent { get; set; }
    public string? CompletedAt { get; set; }
}
