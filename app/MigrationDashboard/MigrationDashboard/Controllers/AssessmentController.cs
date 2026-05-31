using Microsoft.AspNetCore.Mvc;
using MigrationDashboard.Services;
using MigrationDashboard.Models;

namespace MigrationDashboard.Controllers;

public class AssessmentController : Controller
{
    private readonly IPreMigrationAssessmentService _assessmentService;
    private const string OnPremPath = @"D:\cloud-migration-platform\app\OnPremApp\EduFlex - ĐTĐM";

    public AssessmentController(IPreMigrationAssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var viewModel = _assessmentService.RunAssessment(OnPremPath);
        return View(viewModel);
    }

    [HttpPost]
    public IActionResult Run()
    {
        TempData["SuccessMessage"] = "Đã thực hiện cập nhật và đánh giá lại dự án thành công!";
        return RedirectToAction(nameof(Index));
    }
}
