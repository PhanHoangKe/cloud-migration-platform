using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MigrationDashboard.Models;
using Newtonsoft.Json.Linq;

namespace MigrationDashboard.Services;

public class PreMigrationAssessmentService : IPreMigrationAssessmentService
{
    private static readonly string[] ExcludedFolders = { "bin", "obj", "node_modules", ".git", ".vscode", ".github" };
    private readonly OnPremiseAppOptions _options;

    public PreMigrationAssessmentService(Microsoft.Extensions.Options.IOptions<OnPremiseAppOptions> options)
    {
        _options = options.Value;
    }

    public PreMigrationAssessmentViewModel RunAssessment(string? onPremAppPath = null)
    {
        onPremAppPath = onPremAppPath ?? _options.SourcePath;
        var viewModel = new PreMigrationAssessmentViewModel
        {
            OnPremAppPath = onPremAppPath,
            IsScanned = true,
            ProjectName = _options.DisplayName
        };

        if (string.IsNullOrEmpty(onPremAppPath) || !Directory.Exists(onPremAppPath))
        {
            viewModel.ScanError = $"Thư mục ứng dụng On-Premise không tồn tại hoặc không truy cập được tại đường dẫn: '{onPremAppPath}'";
            viewModel.ReadinessScore = 15;
            viewModel.RecommendedMigrationStrategy = "Re-host";
            viewModel.Metrics.Add(new AssessmentMetricViewModel
            {
                Name = "Cấu hình nguồn",
                Value = "Ngoại tuyến / Không tồn tại",
                Status = "Warning",
                Icon = "bi-exclamation-triangle"
            });
            viewModel.RiskItems.Add(new AssessmentRiskItemViewModel
            {
                Level = "High",
                Description = $"Đường dẫn thư mục nguồn không tồn tại: {onPremAppPath}",
                Recommendation = "Kiểm tra lại cấu hình OnPremiseApp:SourcePath trong tệp appsettings.json."
            });
            return viewModel;
        }

        bool hasCsproj = false;
        bool hasValidFramework = false;
        bool hasAppSettings = false;
        bool hasConnectionString = false;
        bool hasControllersOrPages = false;
        bool hasViews = false;
        bool hasWwwroot = false;
        bool canReadSourceWithoutError = true;
        bool isSqlServer = false;

        // 1. Detect Csproj and Target Framework
        try
        {
            var csprojFiles = Directory.GetFiles(onPremAppPath, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojFiles.Length > 0)
            {
                hasCsproj = true;
                var csprojPath = csprojFiles[0];
                viewModel.ProjectName = Path.GetFileNameWithoutExtension(csprojPath);

                string csprojContent = File.ReadAllText(csprojPath);
                var match = Regex.Match(csprojContent, @"<TargetFramework>(.*?)<\/TargetFramework>");
                if (match.Success)
                {
                    viewModel.TargetFramework = match.Groups[1].Value.Trim();
                    if (viewModel.TargetFramework.Contains("net8.0") || viewModel.TargetFramework.Contains("net9.0"))
                    {
                        hasValidFramework = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            canReadSourceWithoutError = false;
            viewModel.ScanError = $"Lỗi đọc tệp .csproj: {ex.Message}";
        }

        // 2. Scan appsettings.json & database connections
        try
        {
            var appSettingsPath = Path.Combine(onPremAppPath, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                hasAppSettings = true;
                viewModel.HasAppSettings = true;

                string jsonContent = File.ReadAllText(appSettingsPath);
                if (!string.IsNullOrWhiteSpace(jsonContent))
                {
                    JObject config = JObject.Parse(jsonContent);
                    var connStrings = config["ConnectionStrings"] ?? config["connectionStrings"];
                    
                    if (connStrings != null && connStrings.HasValues)
                    {
                        hasConnectionString = true;
                        viewModel.HasConnectionString = true;

                        // Check the actual connection string values
                        foreach (var prop in connStrings.Children<JProperty>())
                        {
                            string connStrVal = prop.Value.ToString();
                            if (connStrVal.Contains("sqlserver", StringComparison.OrdinalIgnoreCase) ||
                                connStrVal.Contains("sqlexpress", StringComparison.OrdinalIgnoreCase) ||
                                connStrVal.Contains("trusted_connection", StringComparison.OrdinalIgnoreCase) ||
                                connStrVal.Contains("database=", StringComparison.OrdinalIgnoreCase) ||
                                connStrVal.Contains("server=", StringComparison.OrdinalIgnoreCase) ||
                                connStrVal.Contains("1433", StringComparison.OrdinalIgnoreCase))
                            {
                                isSqlServer = true;
                                viewModel.DatabaseType = "SQL Server";
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            canReadSourceWithoutError = false;
            viewModel.ScanError = (viewModel.ScanError ?? "") + $" Lỗi phân tích appsettings.json: {ex.Message}";
        }

        // 3. Count controllers, pages, views
        var controllersPath = Path.Combine(onPremAppPath, "Controllers");
        var pagesPath = Path.Combine(onPremAppPath, "Pages");
        var viewsPath = Path.Combine(onPremAppPath, "Views");
        var wwwrootPath = Path.Combine(onPremAppPath, "wwwroot");

        if (Directory.Exists(controllersPath))
        {
            hasControllersOrPages = true;
            viewModel.ControllersCount = Directory.GetFiles(controllersPath, "*.cs", SearchOption.AllDirectories).Length;
        }
        else if (Directory.Exists(pagesPath))
        {
            hasControllersOrPages = true;
            viewModel.ControllersCount = Directory.GetFiles(pagesPath, "*.cs", SearchOption.AllDirectories).Length;
        }

        if (Directory.Exists(viewsPath))
        {
            hasViews = true;
            viewModel.ViewsCount = Directory.GetFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories).Length;
        }

        if (Directory.Exists(wwwrootPath))
        {
            hasWwwroot = true;
            viewModel.WwwrootFilesCount = Directory.GetFiles(wwwrootPath, "*.*", SearchOption.AllDirectories).Length;
        }

        // 4. Recursive source scanning for metrics and size
        int totalSourceFiles = 0;
        double totalSizeInBytes = 0;
        bool usesLocalFileStorage = Directory.Exists(Path.Combine(onPremAppPath, "wwwroot", "uploads")) ||
                                    Directory.Exists(Path.Combine(onPremAppPath, "uploads"));

        try
        {
            ScanDirectorySource(onPremAppPath, ref totalSourceFiles, ref totalSizeInBytes, ref usesLocalFileStorage);
            viewModel.TotalSourceFiles = totalSourceFiles;
            viewModel.EstimatedSourceSizeMb = Math.Round(totalSizeInBytes / (1024.0 * 1024.0), 2);
            viewModel.UsesLocalFileStorage = usesLocalFileStorage;
        }
        catch (Exception ex)
        {
            canReadSourceWithoutError = false;
            viewModel.ScanError = (viewModel.ScanError ?? "") + $" Lỗi quét mã nguồn: {ex.Message}";
        }

        // 5. Recommended migration strategy
        if (viewModel.DatabaseType == "SQL Server" && hasCsproj)
        {
            viewModel.RecommendedMigrationStrategy = "Re-platform";
        }
        else
        {
            viewModel.RecommendedMigrationStrategy = "Re-host";
        }

        // 6. Calculate Readiness Score
        int score = 0;
        if (hasCsproj) score += 15;
        if (hasValidFramework) score += 15;
        if (hasAppSettings) score += 10;
        if (hasConnectionString) score += 10;
        if (hasControllersOrPages) score += 10;
        if (hasViews) score += 10;
        if (hasWwwroot) score += 10;
        if (canReadSourceWithoutError) score += 10;
        if (isSqlServer) score += 10;

        viewModel.ReadinessScore = score;

        // 7. Risks analysis
        if (hasConnectionString && isSqlServer)
        {
            viewModel.RiskItems.Add(new AssessmentRiskItemViewModel
            {
                Level = "Medium",
                Description = "Ứng dụng đang phụ thuộc vào cơ sở dữ liệu SQL Server local/nội bộ.",
                Recommendation = "Cần chuyển đổi sang RDS SQL Server hoặc SQL Server chạy trên EC2, và cấu hình lại chuỗi kết nối (Connection String) sang Endpoint của Cloud."
            });
        }

        if (usesLocalFileStorage)
        {
            viewModel.RiskItems.Add(new AssessmentRiskItemViewModel
            {
                Level = "High",
                Description = "Ứng dụng đang lưu trữ tệp tải lên (uploads) trực tiếp trên thư mục local của máy chủ.",
                Recommendation = "Cần chuyển đổi lưu trữ tệp sang AWS S3 Object Storage để đảm bảo khả năng mở rộng (auto-scaling) và tính bền vững."
            });
        }

        if (!hasAppSettings)
        {
            viewModel.RiskItems.Add(new AssessmentRiskItemViewModel
            {
                Level = "High",
                Description = "Không tìm thấy tệp cấu hình appsettings.json trong thư mục dự án.",
                Recommendation = "Bổ sung appsettings.json để tách biệt cấu hình môi trường (Development/Production) ra khỏi mã nguồn ứng dụng."
            });
        }

        if (viewModel.EstimatedSourceSizeMb > 10.0)
        {
            viewModel.RiskItems.Add(new AssessmentRiskItemViewModel
            {
                Level = "Low",
                Description = "Mã nguồn ứng dụng có dung lượng tương đối lớn (> 10 MB).",
                Recommendation = "Kiểm tra và cấu hình bỏ qua các thư mục tạm (bin/obj/node_modules) khi đóng gói backup để tối ưu hóa thời gian tải lên Cloud."
            });
        }

        // 8. Recommendations
        viewModel.Recommendations.Add("Sử dụng Terraform để định nghĩa và tự động hóa việc khởi tạo hạ tầng Cloud giả lập.");
        viewModel.Recommendations.Add("Chuyển đổi tệp sao lưu dữ liệu SQL Server dạng .bak (được nén thành JSON) lên AWS S3.");
        viewModel.Recommendations.Add("Ghi và đồng bộ lịch sử, nhật ký chuyển đổi ứng dụng vào DynamoDB table.");
        viewModel.Recommendations.Add("Thiết lập AWS Lambda Function để thực hiện tự động kiểm thử kết nối (Self-test) sau khi di trú thành công.");
        viewModel.Recommendations.Add("Tách biệt và bảo mật thông tin tài khoản, chuỗi kết nối bằng cách sử dụng Biến môi trường hoặc AWS Secrets Manager thay vì ghi cứng trong code.");

        // 9. Metrics
        viewModel.Metrics.Add(new AssessmentMetricViewModel
        {
            Name = "Framework Target",
            Value = viewModel.TargetFramework,
            Status = hasValidFramework ? "Success" : "Warning",
            Icon = "bi-braces"
        });

        viewModel.Metrics.Add(new AssessmentMetricViewModel
        {
            Name = "Cơ sở dữ liệu nguồn",
            Value = viewModel.DatabaseType != "N/A" ? viewModel.DatabaseType : "Không xác định",
            Status = isSqlServer ? "Success" : "Warning",
            Icon = "bi-database"
        });

        viewModel.Metrics.Add(new AssessmentMetricViewModel
        {
            Name = "Controllers / Pages",
            Value = $"{viewModel.ControllersCount} tệp",
            Status = hasControllersOrPages ? "Success" : "Info",
            Icon = "bi-cpu"
        });

        viewModel.Metrics.Add(new AssessmentMetricViewModel
        {
            Name = "Views (.cshtml)",
            Value = $"{viewModel.ViewsCount} tệp",
            Status = hasViews ? "Success" : "Info",
            Icon = "bi-eye"
        });

        viewModel.Metrics.Add(new AssessmentMetricViewModel
        {
            Name = "Thư mục wwwroot",
            Value = $"{viewModel.WwwrootFilesCount} tệp tin",
            Status = hasWwwroot ? "Success" : "Info",
            Icon = "bi-folder"
        });

        viewModel.Metrics.Add(new AssessmentMetricViewModel
        {
            Name = "Mã nguồn & Cấu hình",
            Value = $"{viewModel.TotalSourceFiles} tệp ({viewModel.EstimatedSourceSizeMb} MB)",
            Status = "Success",
            Icon = "bi-file-code"
        });

        return viewModel;
    }

    private void ScanDirectorySource(string dir, ref int totalSourceFiles, ref double totalSizeInBytes, ref bool usesLocalFileStorage)
    {
        string dirName = Path.GetFileName(dir);
        if (ExcludedFolders.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            return;

        try
        {
            if (dirName.Equals("uploads", StringComparison.OrdinalIgnoreCase))
            {
                usesLocalFileStorage = true;
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".cs" || ext == ".cshtml" || ext == ".json" || ext == ".css" || ext == ".js")
                {
                    totalSourceFiles++;
                    FileInfo fi = new FileInfo(file);
                    totalSizeInBytes += fi.Length;
                }
            }

            foreach (var subDir in Directory.GetDirectories(dir))
            {
                ScanDirectorySource(subDir, ref totalSourceFiles, ref totalSizeInBytes, ref usesLocalFileStorage);
            }
        }
        catch
        {
            // Ignore subfolder read exceptions to keep scan robust
        }
    }
}
