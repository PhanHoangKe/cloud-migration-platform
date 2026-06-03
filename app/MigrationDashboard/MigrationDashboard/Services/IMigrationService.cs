using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IMigrationService
{
    Task<MigrationResultViewModel> StartMigrationAsync(string? migrationId = null);
    Task<MigrationResultViewModel> RollbackMigrationAsync();
    MigrationResultViewModel? GetMigrationResult(string migrationId);
}
