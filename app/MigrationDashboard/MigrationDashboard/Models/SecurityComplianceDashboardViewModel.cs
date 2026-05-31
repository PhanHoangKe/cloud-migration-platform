using System.Collections.Generic;

namespace MigrationDashboard.Models;

public class SecurityCheckItemViewModel
{
    public string Group { get; set; } = string.Empty; // S3, DynamoDB, IAM, Lambda, Terraform
    public string CheckName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // PASSED, WARNING, FAILED, NOT_CHECKED
    public string Severity { get; set; } = string.Empty; // CRITICAL, HIGH, MEDIUM, LOW
    public string Description { get; set; } = string.Empty;
}

public class IamPolicySummaryViewModel
{
    public string RoleName { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string Principal { get; set; } = string.Empty;
    public List<string> AllowedActions { get; set; } = new();
    public string Resource { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // e.g. "Least Privilege Apply"
}

public class SecurityComplianceDashboardViewModel
{
    public int ComplianceScore { get; set; }
    public string StatusBadge { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public List<SecurityCheckItemViewModel> CheckItems { get; set; } = new();
    public List<IamPolicySummaryViewModel> IamPolicySummaries { get; set; } = new();
    public bool IsLocalStackAvailable { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
