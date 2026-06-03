using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IMigrationHistoryService
{
    Task<MigrationHistoryViewModel> GetMigrationHistoryAsync();
}
