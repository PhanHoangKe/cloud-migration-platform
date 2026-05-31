namespace MigrationDashboard.Models;

public class AssessmentMetricViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Status { get; set; } = "Info"; // Success, Warning, Danger, Info
    public string Icon { get; set; } = "bi-info-circle";
}
