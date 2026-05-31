namespace MigrationDashboard.Models;

public class AssessmentRiskItemViewModel
{
    public string Level { get; set; } = "Low"; // High, Medium, Low
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
