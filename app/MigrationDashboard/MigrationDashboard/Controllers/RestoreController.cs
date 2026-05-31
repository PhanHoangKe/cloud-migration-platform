using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;

namespace MigrationDashboard.Controllers;

public class RestoreController : Controller
{
    private readonly IRestoreSimulationService _restoreService;

    public RestoreController(IRestoreSimulationService restoreService)
    {
        _restoreService = restoreService;
    }

    [HttpPost]
    public async Task<IActionResult> Start()
    {
        var result = await _restoreService.RunRestoreSimulationAsync();
        return View("Result", result);
    }
}
