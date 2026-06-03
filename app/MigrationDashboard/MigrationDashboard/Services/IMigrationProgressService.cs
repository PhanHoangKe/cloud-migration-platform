using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IMigrationProgressService
{
    Task<MigrationProgressViewModel?> GetProgressAsync(string migrationId);
}
