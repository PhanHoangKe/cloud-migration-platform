using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;

namespace MigrationDashboard.Controllers;

public class SecurityController : Controller
{
    private readonly ISecurityComplianceService _securityComplianceService;

    public SecurityController(ISecurityComplianceService securityComplianceService)
    {
        _securityComplianceService = securityComplianceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await _securityComplianceService.GetSecurityComplianceDataAsync();
        return View(model);
    }
}
