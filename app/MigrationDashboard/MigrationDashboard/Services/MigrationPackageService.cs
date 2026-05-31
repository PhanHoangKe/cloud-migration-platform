using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class MigrationPackageService : IMigrationPackageService
{
    private readonly IPreMigrationAssessmentService _assessmentService;

    public MigrationPackageService(IPreMigrationAssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    public async Task<PreMigrationAssessmentViewModel> GenerateAssessmentReportAsync(string migrationId, string onPremPath, string targetFilePath)
    {
        var report = _assessmentService.RunAssessment(onPremPath);

        var payload = new
        {
            migrationId = migrationId,
            generatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            sourceAppPath = report.OnPremAppPath,
            projectName = report.ProjectName,
            targetFramework = report.TargetFramework,
            databaseType = report.DatabaseType,
            readinessScore = report.ReadinessScore,
            risks = report.RiskItems,
            recommendations = report.Recommendations,
            metrics = report.Metrics
        };

        string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        await File.WriteAllTextAsync(targetFilePath, json, Encoding.UTF8);

        return report;
    }

    public async Task GenerateManifestAsync(string migrationId, string onPremPath, string targetFilePath, string sourceZipName, string reportName)
    {
        var csprojName = "N/A";
        try
        {
            var files = Directory.GetFiles(onPremPath, "*.csproj");
            if (files.Length > 0)
            {
                csprojName = Path.GetFileNameWithoutExtension(files[0]);
            }
        }
        catch { }

        var payload = new
        {
            migrationId = migrationId,
            createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            sourceApplication = csprojName,
            sourcePath = onPremPath,
            migrationStrategy = "Re-platform",
            cloudTarget = "LocalStack AWS Simulation",
            services = new[] { "S3", "DynamoDB", "Lambda", "IAM", "Terraform" },
            files = new[] { reportName, sourceZipName },
            packageVersion = "1.0",
            academicNote = "This package is generated for academic cloud migration simulation."
        };

        string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        await File.WriteAllTextAsync(targetFilePath, json, Encoding.UTF8);
    }

    public async Task<string> CreateSourcePackageZipAsync(string onPremPath, string targetFilePath)
    {
        if (!Directory.Exists(onPremPath))
        {
            throw new DirectoryNotFoundException($"Thư mục nguồn không tồn tại tại: {onPremPath}");
        }

        if (File.Exists(targetFilePath))
        {
            File.Delete(targetFilePath);
        }

        await Task.Run(() =>
        {
            using (var zipStream = new FileStream(targetFilePath, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var files = Directory.GetFiles(onPremPath, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        string relativePath = Path.GetRelativePath(onPremPath, file);
                        
                        // Check if relative path contains excluded folders
                        bool isExcluded = false;
                        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        foreach (var part in parts)
                        {
                            if (part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                                part.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                                part.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                                part.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                                part.Equals("logs", StringComparison.OrdinalIgnoreCase))
                            {
                                isExcluded = true;
                                break;
                            }
                        }

                        if (isExcluded) continue;

                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".user" || ext == ".suo") continue;

                        bool isWwwroot = relativePath.Contains("wwwroot", StringComparison.OrdinalIgnoreCase);

                        if (isWwwroot || ext == ".cs" || ext == ".cshtml" || ext == ".json" || ext == ".css" || ext == ".js" || ext == ".csproj")
                        {
                            string entryName = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                            archive.CreateEntryFromFile(file, entryName);
                        }
                    }
                }
            }
        });

        FileInfo fi = new FileInfo(targetFilePath);
        double sizeInMb = fi.Length / (1024.0 * 1024.0);
        return $"{Math.Round(sizeInMb, 2)} MB";
    }
}
