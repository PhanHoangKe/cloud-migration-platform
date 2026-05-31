using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IMigrationPackageService
{
    Task<PreMigrationAssessmentViewModel> GenerateAssessmentReportAsync(string migrationId, string onPremPath, string targetFilePath);
    Task GenerateManifestAsync(string migrationId, string onPremPath, string targetFilePath, string sourceZipName, string reportName);
    Task<string> CreateSourcePackageZipAsync(string onPremPath, string targetFilePath);
}
