using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IRestoreSimulationService
{
    Task<RestoreResultViewModel> RunRestoreSimulationAsync();
}
