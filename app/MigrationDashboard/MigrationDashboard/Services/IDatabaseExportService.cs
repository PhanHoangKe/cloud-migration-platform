using System.Threading.Tasks;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public interface IDatabaseExportService
{
    Task<DatabaseExportResultViewModel> ExportDatabaseAsync(string migrationId);
}
