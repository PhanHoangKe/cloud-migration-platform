using System;

namespace MigrationDashboard.Models;

public class CloudHealthCheckItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown"; // Healthy, Warning, Critical, Unknown
    public string StatusText { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}
