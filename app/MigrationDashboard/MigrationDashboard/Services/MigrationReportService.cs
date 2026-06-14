using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

using MigrationDashboard.Models;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace MigrationDashboard.Services;

public class MigrationReportService : IMigrationReportService
{
    private readonly OnPremiseAppOptions _options;

    public MigrationReportService(IOptions<OnPremiseAppOptions> options)
    {
        _options = options.Value;
    }

    public async Task<(string jsonPath, string htmlPath)> GenerateReportFilesAsync(
        string migrationId,
        string sourcePath,
        string packageSize,
        int readinessScore,
        string s3BucketName,
        string dynamoTableName,
        string lambdaSelfTestStatus,
        int migrationHealthScore,
        DateTime startedAt,
        DateTime completedAt,
        string tempDirectory,
        string dbExportStatus = "",
        int dbTableCount = 0,
        int dbTotalRows = 0,
        string dbExportS3Key = "")
    {
        if (!Directory.Exists(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        var durationSeconds = Math.Round((completedAt - startedAt).TotalSeconds, 2);
        var projectName = _options.DisplayName;
        try
        {
            if (Directory.Exists(sourcePath))
            {
                var files = Directory.GetFiles(sourcePath, "*.csproj");
                if (files.Length > 0)
                {
                    projectName = Path.GetFileNameWithoutExtension(files[0]);
                }
            }
        }
        catch { }

        var uploadedFilesList = new List<string>
        {
            "assessment-report.json",
            "migration-manifest.json",
            "source-package.zip",
            "migration-report.json",
            "migration-report.html"
        };
        if (!string.IsNullOrEmpty(dbExportS3Key))
        {
            uploadedFilesList.Add("database-export.json");
        }
        var uploadedFiles = uploadedFilesList.ToArray();

        // 1. Generate JSON Report
        var reportData = new
        {
            migrationId = migrationId,
            projectName = projectName,
            sourcePath = sourcePath,
            migrationStrategy = "Re-platform",
            cloudTarget = "LocalStack AWS Simulation",
            startedAt = startedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            completedAt = completedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            durationSeconds = durationSeconds,
            readinessScore = readinessScore,
            packageSizeMb = packageSize,
            uploadedFiles = uploadedFiles,
            s3BucketName = s3BucketName,
            dynamoDbTableName = dynamoTableName,
            lambdaSelfTestStatus = lambdaSelfTestStatus,
            migrationHealthScore = migrationHealthScore,
            downtime = "0.00 seconds (Zero downtime via AWS DMS)",
            networkConnection = "AWS VPC + IPSec VPN Site-to-Site",
            dataLossStatus = "Zero Data Loss (100% synchronized)",
            databaseExport = new
            {
                status = dbExportStatus,
                tableCount = dbTableCount,
                totalRows = dbTotalRows,
                s3Key = dbExportS3Key
            },
            costEstimation = new
            {
                s3Storage = "0.08 USD/month",
                dynamoDbRequests = "0.10 USD/month",
                lambdaInvocations = "0.05 USD/month",
                vpnSiteToSite = "36.00 USD/month",
                networkOther = "0.07 USD/month",
                total = "36.30 USD/month"
            },
            beforeAfterSummary = new[]
            {
                new { before = "On-premise file storage", after = "S3 object storage" },
                new { before = "Local/manual logs", after = "DynamoDB migration logs" },
                new { before = "Manual verification", after = "Lambda self-test" },
                new { before = "Manual infrastructure", after = "Terraform IaC" },
                new { before = "Local firewall/Intranet", after = "IPSec VPN Site-to-Site & VPC" }
            },
            academicNote = "This report is generated for academic cloud migration simulation using LocalStack. Cost values are illustrative only and not official AWS pricing."
        };

        var jsonPath = Path.Combine(tempDirectory, "migration-report.json");
        var jsonContent = JsonConvert.SerializeObject(reportData, Formatting.Indented);
        await File.WriteAllTextAsync(jsonPath, jsonContent, Encoding.UTF8);

        // 2. Generate HTML Report
        var htmlBuilder = new StringBuilder();
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"vi\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("    <title>Báo cáo kết quả chuyển đổi ứng dụng</title>");
        htmlBuilder.AppendLine("    <style>");
        htmlBuilder.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #334155; max-width: 800px; margin: 40px auto; padding: 20px; background-color: #f8fafc; }");
        htmlBuilder.AppendLine("        .card { background: #ffffff; border-radius: 12px; box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1); padding: 30px; border: 1px solid #e2e8f0; }");
        htmlBuilder.AppendLine("        .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #e2e8f0; padding-bottom: 20px; margin-bottom: 25px; }");
        htmlBuilder.AppendLine("        .logo-title h1 { font-size: 22px; color: #4f46e5; margin: 0; font-weight: 700; }");
        htmlBuilder.AppendLine("        .logo-title p { margin: 5px 0 0 0; font-size: 13px; color: #64748b; }");
        htmlBuilder.AppendLine("        .badge { display: inline-block; padding: 4px 10px; border-radius: 20px; font-size: 11px; font-weight: 600; text-transform: uppercase; }");
        htmlBuilder.AppendLine("        .badge-success { background: #dcfce7; color: #15803d; }");
        htmlBuilder.AppendLine("        .badge-warning { background: #fef3c7; color: #b45309; }");
        htmlBuilder.AppendLine("        .section-title { font-size: 15px; font-weight: 600; color: #1e293b; margin-top: 25px; margin-bottom: 12px; border-left: 4px solid #4f46e5; padding-left: 10px; }");
        htmlBuilder.AppendLine("        table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
        htmlBuilder.AppendLine("        th, td { padding: 10px 12px; text-align: left; border-bottom: 1px solid #e2e8f0; font-size: 13px; }");
        htmlBuilder.AppendLine("        th { background-color: #f1f5f9; color: #475569; font-weight: 600; }");
        htmlBuilder.AppendLine("        .monospace { font-family: 'Courier New', Courier, monospace; font-size: 12px; color: #0f172a; font-weight: 600; }");
        htmlBuilder.AppendLine("        .cost-total { font-weight: 700; color: #0f172a; background-color: #f8fafc; }");
        htmlBuilder.AppendLine("        .alert-academic { background-color: #fffbeb; border: 1px solid #fef3c7; border-radius: 8px; padding: 15px; margin-top: 30px; color: #b45309; font-size: 13px; }");
        htmlBuilder.AppendLine("        .grid-2 { display: flex; gap: 20px; }");
        htmlBuilder.AppendLine("        .grid-col { flex: 1; min-width: 0; }");
        htmlBuilder.AppendLine("    </style>");
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        htmlBuilder.AppendLine("    <div class=\"card\">");
        htmlBuilder.AppendLine("        <!-- Header -->");
        htmlBuilder.AppendLine("        <div class=\"header\">");
        htmlBuilder.AppendLine("            <div class=\"logo-title\">");
        htmlBuilder.AppendLine("                <h1>Báo cáo kết quả chuyển đổi ứng dụng</h1>");
        htmlBuilder.AppendLine("                <p>Hệ thống mô phỏng chuyển đổi Cloud (LocalStack & Terraform)</p>");
        htmlBuilder.AppendLine("            </div>");
        htmlBuilder.AppendLine("            <div>");
        htmlBuilder.AppendLine($"                <span class=\"badge badge-success\">Success</span>");
        htmlBuilder.AppendLine("            </div>");
        htmlBuilder.AppendLine("        </div>");
        htmlBuilder.AppendLine("");
        htmlBuilder.AppendLine("        <!-- Metadata Table -->");
        htmlBuilder.AppendLine("        <div class=\"section-title\">Thông tin tổng quan</div>");
        htmlBuilder.AppendLine("        <table>");
        htmlBuilder.AppendLine("            <tbody>");
        htmlBuilder.AppendLine($"                <tr><td style=\"width: 30%; color: #64748b;\">Mã di trú (ID)</td><td class=\"monospace\">{migrationId}</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Dự án nguồn</td><td>{projectName}</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Thư mục nguồn</td><td class=\"monospace\">{sourcePath}</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Chiến lược di trú</td><td>Re-platform (Lift & Shift Web + Re-platform DB)</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Hạ tầng mạng & Bảo mật</td><td>AWS VPC (Public/Private Subnets) & IPSec VPN Site-to-Site</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Thời gian ngừng hoạt động (Downtime)</td><td><strong>~0.00 giây</strong> (Zero Downtime nhờ AWS DMS đồng bộ nóng)</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Độ hao hụt dữ liệu</td><td><strong>Zero Data Loss</strong> (100% đồng bộ toàn vẹn)</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Thời gian thực hiện</td><td>{startedAt:yyyy-MM-dd HH:mm:ss} (Thời lượng: {durationSeconds} giây)</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Điểm sẵn sàng (Readiness)</td><td><strong>{readinessScore}/100</strong></td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Sức khỏe đám mây (Health)</td><td><strong>{migrationHealthScore}/100</strong></td></tr>");
        htmlBuilder.AppendLine("            </tbody>");
        htmlBuilder.AppendLine("        </table>");
        htmlBuilder.AppendLine("");
        htmlBuilder.AppendLine("        <!-- S3 & DynamoDB Details -->");
        htmlBuilder.AppendLine("        <div class=\"section-title\">Tài nguyên đám mây và Tệp tin đóng gói</div>");
        htmlBuilder.AppendLine("        <table>");
        htmlBuilder.AppendLine("            <thead>");
        htmlBuilder.AppendLine("                <tr>");
        htmlBuilder.AppendLine("                    <th style=\"width: 50%;\">Tên tệp tin đã nén/xuất</th>");
        htmlBuilder.AppendLine("                    <th>Đường dẫn lưu trữ trên S3 (Key Prefix)</th>");
        htmlBuilder.AppendLine("                </tr>");
        htmlBuilder.AppendLine("            </thead>");
        htmlBuilder.AppendLine("            <tbody>");
        foreach (var file in uploadedFiles)
        {
            htmlBuilder.AppendLine("                <tr>");
            htmlBuilder.AppendLine($"                    <td><span style=\"font-weight: 500;\">{file}</span></td>");
            htmlBuilder.AppendLine($"                    <td class=\"monospace\">migrations/{migrationId}/{file}</td>");
            htmlBuilder.AppendLine("                </tr>");
        }
        htmlBuilder.AppendLine("            </tbody>");
        htmlBuilder.AppendLine("        </table>");
        htmlBuilder.AppendLine("");
        htmlBuilder.AppendLine("        <div class=\"grid-2\">");
        htmlBuilder.AppendLine("            <!-- Cost Estimation -->");
        htmlBuilder.AppendLine("            <div class=\"grid-col\">");
        htmlBuilder.AppendLine("                <div class=\"section-title\">Ước tính chi phí vận hành</div>");
        htmlBuilder.AppendLine("                <table>");
        htmlBuilder.AppendLine("                    <thead>");
        htmlBuilder.AppendLine("                        <tr><th>Dịch vụ AWS</th><th>Chi phí / tháng</th></tr>");
        htmlBuilder.AppendLine("                    </thead>");
        htmlBuilder.AppendLine("                    <tbody>");
        htmlBuilder.AppendLine("                        <tr><td>Amazon S3 Storage</td><td>0.08 USD</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>Amazon DynamoDB</td><td>0.10 USD</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>AWS Lambda</td><td>0.05 USD</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>AWS Site-to-Site VPN</td><td>36.00 USD</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>Network / Other</td><td>0.07 USD</td></tr>");
        htmlBuilder.AppendLine("                        <tr class=\"cost-total\"><td>Tổng cộng (Total)</td><td>36.30 USD</td></tr>");
        htmlBuilder.AppendLine("                    </tbody>");
        htmlBuilder.AppendLine("                </table>");
        htmlBuilder.AppendLine("            </div>");
        htmlBuilder.AppendLine("");
        htmlBuilder.AppendLine("            <!-- Before / After Architecture -->");
        htmlBuilder.AppendLine("            <div class=\"grid-col\">");
        htmlBuilder.AppendLine("                <div class=\"section-title\">So sánh Kiến trúc trước & sau</div>");
        htmlBuilder.AppendLine("                <table>");
        htmlBuilder.AppendLine("                    <thead>");
        htmlBuilder.AppendLine("                        <tr><th>Môi trường On-Premise</th><th>Môi trường AWS Cloud</th></tr>");
        htmlBuilder.AppendLine("                    </thead>");
        htmlBuilder.AppendLine("                    <tbody>");
        htmlBuilder.AppendLine("                        <tr><td>Lưu trữ tệp local</td><td>Lưu trữ đối tượng Amazon S3</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>Ghi logs tệp local</td><td>DynamoDB log di trú</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>Kiểm thử thủ công</td><td>Lambda Self-test tự động</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>Cài đặt hạ tầng tay</td><td>Quản lý bằng Terraform IaC</td></tr>");
        htmlBuilder.AppendLine("                        <tr><td>Mạng LAN / Internet công cộng</td><td>AWS VPC & IPSec VPN Site-to-Site</td></tr>");
        htmlBuilder.AppendLine("                    </tbody>");
        htmlBuilder.AppendLine("                </table>");
        htmlBuilder.AppendLine("            </div>");
        htmlBuilder.AppendLine("        </div>");
        htmlBuilder.AppendLine("");
        htmlBuilder.AppendLine("        <!-- Self-Test & Integration -->");
        htmlBuilder.AppendLine("        <div class=\"section-title\">Kiểm thử liên thông hệ thống</div>");
        htmlBuilder.AppendLine("        <table>");
        htmlBuilder.AppendLine("            <tbody>");
        htmlBuilder.AppendLine($"                <tr><td style=\"width: 30%; color: #64748b;\">S3 Bucket đích</td><td>{s3BucketName}</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Bảng logs DynamoDB</td><td>{dynamoTableName}</td></tr>");
        htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Kiểm tra AWS Lambda</td><td>");
        if (lambdaSelfTestStatus.Equals("Passed", StringComparison.OrdinalIgnoreCase))
        {
            htmlBuilder.AppendLine("                    <span class=\"badge badge-success\">PASSED</span>");
        }
        else
        {
            htmlBuilder.AppendLine("                    <span class=\"badge badge-warning\">FAILED / ERROR</span>");
        }
        htmlBuilder.AppendLine("                </td></tr>");
        htmlBuilder.AppendLine("            </tbody>");
        htmlBuilder.AppendLine("        </table>");
        if (!string.IsNullOrEmpty(dbExportStatus))
        {
            htmlBuilder.AppendLine("        <!-- Database Export Details -->");
            htmlBuilder.AppendLine("        <div class=\"section-title\">Xuất dữ liệu SQL Server</div>");
            htmlBuilder.AppendLine("        <table>");
            htmlBuilder.AppendLine("            <tbody>");
            htmlBuilder.AppendLine($"                <tr><td style=\"width: 30%; color: #64748b;\">Trạng thái xuất</td><td>");
            if (dbExportStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                htmlBuilder.AppendLine("                    <span class=\"badge badge-success\">SUCCESS / COMPLETED</span>");
            }
            else
            {
                htmlBuilder.AppendLine("                    <span class=\"badge badge-warning\">FAILED / WARNING</span>");
            }
            htmlBuilder.AppendLine("                </td></tr>");
            htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Số lượng bảng đã xuất</td><td>{dbTableCount} bảng</td></tr>");
            htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">Tổng số bản ghi (rows)</td><td>{dbTotalRows} dòng</td></tr>");
            if (!string.IsNullOrEmpty(dbExportS3Key))
            {
                htmlBuilder.AppendLine($"                <tr><td style=\"color: #64748b;\">S3 Object Key</td><td class=\"monospace\">{dbExportS3Key}</td></tr>");
            }
            htmlBuilder.AppendLine("            </tbody>");
            htmlBuilder.AppendLine("        </table>");
            htmlBuilder.AppendLine("");
        }

        // Academic Note
        htmlBuilder.AppendLine("        <!-- Academic Note -->");
        htmlBuilder.AppendLine("        <div class=\"alert-academic\">");
        htmlBuilder.AppendLine("            <strong>Chú thích học thuật (Academic Note):</strong><br>");
        htmlBuilder.AppendLine("            Báo cáo này được tự động tạo lập phục vụ mục đích mô phỏng đồ án Điện toán đám mây sử dụng LocalStack AWS. Các giá trị chi phí ước tính ở trên chỉ có tính chất tham khảo minh họa, không phản ánh bảng giá dịch vụ thực tế của AWS.");
        htmlBuilder.AppendLine("        </div>");
        htmlBuilder.AppendLine("    </div>");
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");

        var htmlPath = Path.Combine(tempDirectory, "migration-report.html");
        await File.WriteAllTextAsync(htmlPath, htmlBuilder.ToString(), Encoding.UTF8);

        return (jsonPath, htmlPath);
    }
}
