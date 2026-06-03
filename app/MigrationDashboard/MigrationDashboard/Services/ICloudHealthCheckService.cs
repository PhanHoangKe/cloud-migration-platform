using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface ICloudHealthCheckService
{
    Task<CloudHealthCheckViewModel> GetCloudHealthCheckAsync();
}
