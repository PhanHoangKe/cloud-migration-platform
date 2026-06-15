using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class PreMigrationAssessmentViewModel
{
    public string ProjectName { get; set; } = "N/A";
    public string TargetFramework { get; set; } = "N/A";
    public bool HasAppSettings { get; set; }
    public bool HasConnectionString { get; set; }
    public string DatabaseType { get; set; } = "N/A";
    public int ControllersCount { get; set; }
    public int ViewsCount { get; set; }
    public int WwwrootFilesCount { get; set; }
    public int TotalSourceFiles { get; set; }
    public double EstimatedSourceSizeMb { get; set; }
    public bool UsesLocalFileStorage { get; set; }
    public string RecommendedMigrationStrategy { get; set; } = "Re-platform";
    public int ReadinessScore { get; set; }
    
    public List<AssessmentRiskItemViewModel> RiskItems { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<AssessmentMetricViewModel> Metrics { get; set; } = new();
    
    public string OnPremAppPath { get; set; } = string.Empty;
    public string? ScanError { get; set; }
    public bool IsScanned { get; set; }

    // FinOps Cost Estimation Properties
    public decimal RehostWebCost { get; set; }
    public decimal RehostDbCost { get; set; }
    public decimal RehostStorageCost { get; set; }
    public decimal RehostTotalCost { get; set; }

    public decimal ServerlessWebCost { get; set; }
    public decimal ServerlessDbCost { get; set; }
    public decimal ServerlessStorageCost { get; set; }
    public decimal ServerlessTotalCost { get; set; }

    public decimal CostSavingsPercent { get; set; }
}
