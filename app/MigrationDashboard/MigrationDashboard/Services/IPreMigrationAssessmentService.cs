using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IPreMigrationAssessmentService
{
    PreMigrationAssessmentViewModel RunAssessment(string? onPremAppPath = null);
}
