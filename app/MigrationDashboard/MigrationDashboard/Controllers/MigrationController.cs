using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;
using MigrationDashboard.Models;

namespace MigrationDashboard.Controllers;

public class MigrationController : Controller
{
    private readonly IMigrationService _migrationService;
    private readonly IMigrationProgressService _progressService;
    private readonly IMigrationHistoryService _historyService;

    public MigrationController(IMigrationService migrationService, IMigrationProgressService progressService, IMigrationHistoryService historyService)
    {
        _migrationService = migrationService;
        _progressService = progressService;
        _historyService = historyService;
    }

    [HttpPost]
    public IActionResult Start()
    {
        var migrationId = $"MIGRATION_{DateTime.Now:yyyyMMdd_HHmmss}";
        
        var serviceProvider = HttpContext.RequestServices;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
                await migrationService.StartMigrationAsync(migrationId);
            }
            catch (Exception)
            {
                // Suppress background thread exception or log internally
            }
        });

        return RedirectToAction("Result", new { id = migrationId });
    }

    [HttpGet]
    public IActionResult Result(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound("Mã di trú không hợp lệ");
        }

        var result = _migrationService.GetMigrationResult(id);
        if (result == null)
        {
            result = new MigrationResultViewModel
            {
                MigrationId = id,
                Status = "STARTED",
                IsSuccess = false
            };
        }

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Progress(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest(new { error = "Mã di trú không được để trống" });
        }

        var progress = await _progressService.GetProgressAsync(id);
        if (progress == null)
        {
            return NotFound(new { error = $"Không tìm thấy tiến trình cho mã di trú {id}" });
        }

        return Json(progress);
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

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var model = await _historyService.GetMigrationHistoryAsync();
        return View(model);
    }
}
