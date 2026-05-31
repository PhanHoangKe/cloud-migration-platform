using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface ICloudResourceService
{
    Task<CloudResourceDashboardViewModel> GetDashboardDataAsync();
}
