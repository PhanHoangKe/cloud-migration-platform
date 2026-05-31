using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IMigrationService
{
    Task<MigrationResultViewModel> StartMigrationAsync();
    Task<MigrationResultViewModel> RollbackMigrationAsync();
}
