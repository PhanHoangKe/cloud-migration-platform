using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Models;
using MigrationDashboard.Services;

namespace MigrationDashboard.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ICloudResourceService _cloudResourceService;
    private readonly IPreMigrationAssessmentService _assessmentService;
    private readonly ICloudHealthCheckService _healthCheckService;

    public HomeController(ILogger<HomeController> logger, ICloudResourceService cloudResourceService, IPreMigrationAssessmentService assessmentService, ICloudHealthCheckService healthCheckService)
    {
        _logger = logger;
        _cloudResourceService = cloudResourceService;
        _assessmentService = assessmentService;
        _healthCheckService = healthCheckService;
    }

    public async Task<IActionResult> Index()
    {
        var dashboardData = await _cloudResourceService.GetDashboardDataAsync();
        dashboardData.Assessment = _assessmentService.RunAssessment();

        try
        {
            var healthCheck = await _healthCheckService.GetCloudHealthCheckAsync();
            dashboardData.CloudHealthScore = healthCheck.HealthScore;
            dashboardData.CloudHealthStatus = healthCheck.OverallStatus;
        }
        catch
        {
            dashboardData.CloudHealthScore = 0;
            dashboardData.CloudHealthStatus = "Unknown";
        }

        return View(dashboardData);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
