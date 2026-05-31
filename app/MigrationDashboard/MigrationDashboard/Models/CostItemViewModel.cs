namespace MigrationDashboard.Models;

public class CostItemViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double CostPerMonth { get; set; }
}
