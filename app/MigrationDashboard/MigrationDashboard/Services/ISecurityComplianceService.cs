using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface ISecurityComplianceService
{
    Task<SecurityComplianceDashboardViewModel> GetSecurityComplianceDataAsync();
}
