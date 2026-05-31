using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;

namespace MigrationDashboard.Controllers;

public class MonitoringController : Controller
{
    private readonly IMonitoringService _monitoringService;

    public MonitoringController(IMonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? migrationId, [FromQuery] string? status)
    {
        var model = await _monitoringService.GetMonitoringDataAsync(migrationId, status);
        return View(model);
    }
}
