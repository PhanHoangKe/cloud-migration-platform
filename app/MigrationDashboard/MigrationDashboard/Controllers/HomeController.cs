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

    public HomeController(ILogger<HomeController> logger, ICloudResourceService cloudResourceService, IPreMigrationAssessmentService assessmentService)
    {
        _logger = logger;
        _cloudResourceService = cloudResourceService;
        _assessmentService = assessmentService;
    }

    public async Task<IActionResult> Index()
    {
        var dashboardData = await _cloudResourceService.GetDashboardDataAsync();
        dashboardData.Assessment = _assessmentService.RunAssessment();
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
