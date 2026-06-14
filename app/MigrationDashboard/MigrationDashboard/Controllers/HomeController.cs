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
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, ICloudResourceService cloudResourceService, IPreMigrationAssessmentService assessmentService, ICloudHealthCheckService healthCheckService, IConfiguration configuration)
    {
        _logger = logger;
        _cloudResourceService = cloudResourceService;
        _assessmentService = assessmentService;
        _healthCheckService = healthCheckService;
        _configuration = configuration;
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

    [HttpPost]
    public async Task<IActionResult> TestDbConnection([FromBody] DbConnectionSettingsModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.Server) || string.IsNullOrEmpty(model.Database))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin Server và Database!" });
        }

        string connectionString;
        if (model.AuthType == "windows")
        {
            connectionString = $"Server={model.Server};Database={model.Database};Trusted_Connection=True;TrustServerCertificate={model.TrustServerCertificate.ToString().ToLower()};";
        }
        else
        {
            connectionString = $"Server={model.Server};Database={model.Database};User Id={model.Username};Password={model.Password};TrustServerCertificate={model.TrustServerCertificate.ToString().ToLower()};";
        }

        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();
            return Json(new { success = true, message = $"Kết nối thành công! Đã kết nối tới cơ sở dữ liệu '{model.Database}' trên server '{model.Server}'." });
        }
        catch (System.Exception ex)
        {
            return Json(new { success = false, message = $"Kết nối thất bại: {ex.Message}" });
        }
    }

    [HttpPost]
    public IActionResult SaveDbConnection([FromBody] DbConnectionSettingsModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.Server) || string.IsNullOrEmpty(model.Database))
        {
            return Json(new { success = false, message = "Cấu hình không hợp lệ!" });
        }

        string connectionString;
        if (model.AuthType == "windows")
        {
            connectionString = $"Server={model.Server};Database={model.Database};Trusted_Connection=True;TrustServerCertificate={model.TrustServerCertificate.ToString().ToLower()};";
        }
        else
        {
            connectionString = $"Server={model.Server};Database={model.Database};User Id={model.Username};Password={model.Password};TrustServerCertificate={model.TrustServerCertificate.ToString().ToLower()};";
        }

        DatabaseExportService.CustomConnectionString = connectionString;

        // Auto-update the target project's appsettings.json to align pre-migration assessment
        try
        {
            var sourcePath = _configuration["OnPremiseApp:SourcePath"];
            if (!string.IsNullOrEmpty(sourcePath) && System.IO.Directory.Exists(sourcePath))
            {
                var appSettingsPath = System.IO.Path.Combine(sourcePath, "appsettings.json");
                if (System.IO.File.Exists(appSettingsPath))
                {
                    string jsonContent = System.IO.File.ReadAllText(appSettingsPath);
                    var configObj = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                    
                    var connStrings = configObj["ConnectionStrings"] as Newtonsoft.Json.Linq.JObject;
                    if (connStrings == null)
                    {
                        connStrings = new Newtonsoft.Json.Linq.JObject();
                        configObj["ConnectionStrings"] = connStrings;
                    }
                    
                    connStrings["DefaultConnection"] = connectionString;
                    
                    System.IO.File.WriteAllText(appSettingsPath, configObj.ToString(Newtonsoft.Json.Formatting.Indented));
                }
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật appsettings.json của dự án nguồn");
        }

        return Json(new { success = true, message = "Đã lưu cấu hình kết nối CSDL thành công!" });
    }
}

public class DbConnectionSettingsModel
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string AuthType { get; set; } = "windows";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
}
