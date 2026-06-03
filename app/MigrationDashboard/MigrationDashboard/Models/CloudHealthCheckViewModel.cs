using System;
using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class CloudHealthCheckViewModel
{
    public string OverallStatus { get; set; } = "Unknown";
    public int HealthScore { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public List<CloudHealthCheckItemViewModel> Items { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
