using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;
using MigrationDashboard.Models;

namespace MigrationDashboard.Controllers;

public class MigrationController : Controller
{
    private readonly IMigrationService _migrationService;

    public MigrationController(IMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    [HttpPost]
    public async Task<IActionResult> Start()
    {
        var result = await _migrationService.StartMigrationAsync();
        return View("Result", result);
    }
}
