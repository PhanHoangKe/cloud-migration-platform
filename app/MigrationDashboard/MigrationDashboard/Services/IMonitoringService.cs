using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IMonitoringService
{
    Task<MonitoringDashboardViewModel> GetMonitoringDataAsync(string? migrationIdFilter, string? statusFilter);
}
