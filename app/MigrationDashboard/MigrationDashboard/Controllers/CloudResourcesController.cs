using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;

namespace MigrationDashboard.Controllers;

public class CloudResourcesController : Controller
{
    private readonly ICloudHealthCheckService _healthCheckService;

    public CloudResourcesController(ICloudHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Health()
    {
        var healthCheck = await _healthCheckService.GetCloudHealthCheckAsync();
        return View(healthCheck);
    }

    [HttpGet]
    public async Task<IActionResult> HealthJson()
    {
        var healthCheck = await _healthCheckService.GetCloudHealthCheckAsync();
        return Json(new
        {
            overallStatus = healthCheck.OverallStatus,
            healthScore = healthCheck.HealthScore,
            checkedAt = healthCheck.CheckedAt,
            items = healthCheck.Items
        });
    }
}
