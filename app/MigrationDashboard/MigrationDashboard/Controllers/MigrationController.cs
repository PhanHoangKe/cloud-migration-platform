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

    [HttpPost]
    public async Task<IActionResult> Rollback()
    {
        var result = await _migrationService.RollbackMigrationAsync();
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Khởi chạy Rollback giả lập thành công! Log ROLLBACK_STARTED và ROLLBACK_COMPLETED đã được ghi nhận.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Lỗi Rollback: {result.ErrorMessage}";
        }
        return RedirectToAction("Index", "Home");
    }
}
