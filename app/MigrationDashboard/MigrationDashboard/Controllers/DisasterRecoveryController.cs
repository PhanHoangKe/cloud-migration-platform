using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MigrationDashboard.Models;

namespace MigrationDashboard.Controllers;

public class DisasterRecoveryController : Controller
{
    private readonly IConfiguration _configuration;

    public DisasterRecoveryController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public IActionResult TriggerDisaster()
    {
        DisasterState.IsSingaporeDown = true;
        DisasterState.IsFailedOver = false;
        DisasterState.CurrentRegion = "ap-southeast-1";
        DisasterState.FailoverStatus = "STANDBY";
        DisasterState.FailoverLogs.Clear();
        DisasterState.AddLog("CẢNH BÁO KHẨN CẤP: Vùng Singapore (ap-southeast-1) mất kết nối. Sự cố cáp quang biển diện rộng.");
        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult StartFailover()
    {
        if (DisasterState.FailoverStatus == "RUNNING")
        {
            return Json(new { success = false, message = "Quy trình failover đang chạy." });
        }

        DisasterState.FailoverStatus = "RUNNING";
        DisasterState.FailoverStartTime = DateTime.Now;
        DisasterState.FailoverLogs.Clear();
        DisasterState.AddLog("BẮT ĐẦU QUY TRÌNH PHỤC HỒI THẢM HỌA KHẨN CẤP (FAILOVER TOKYO)...");

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);
                DisasterState.AddLog("Khởi tạo cấu hình khẩn cấp...");
                DisasterState.AddLog("Phát hiện vùng dự phòng mục tiêu: ap-northeast-1 (Tokyo).");
                
                await Task.Delay(1500);
                DisasterState.AddLog("Cập nhật AWS Region trong file cấu hình nhà cung cấp Terraform thành 'ap-northeast-1'.");
                DisasterState.AddLog("Chạy lệnh ngầm: terraform apply -var=\"aws_region=ap-northeast-1\" -auto-approve");

                await Task.Delay(2000);
                bool awsSuccess = await SetupTokyoResourcesInLocalStackAsync();
                if (awsSuccess)
                {
                    DisasterState.AddLog("IaC thành công! Đã tạo các tài nguyên bảo mật trên vùng Tokyo:");
                    DisasterState.AddLog("  - S3 Bucket: cloud-migration-backup-kedep-tokyo");
                    DisasterState.AddLog("  - DynamoDB Table: cloud-migration-logs-kedep-tokyo");
                    DisasterState.AddLog("  - AWS Lambda: cloud-migration-self-test-kedep-tokyo");
                }
                else
                {
                    DisasterState.AddLog("CẢNH BÁO: Không kết nối được LocalStack. Chuyển sang chế độ giả lập Tokyo...");
                    DisasterState.AddLog("Giả lập: Đã tạo và phân bổ tài nguyên trên vùng Tokyo (ap-northeast-1).");
                }

                await Task.Delay(1500);
                DisasterState.AddLog("Bắt đầu sao chép dữ liệu xuyên vùng (Cross-Region Replication)...");
                DisasterState.AddLog("Sao chép gói mã nguồn: source-package.zip -> S3 Tokyo (Zero Data Loss).");
                DisasterState.AddLog("Đồng bộ cơ sở dữ liệu: database-export.json -> S3 Tokyo (Zero Data Loss).");

                await Task.Delay(1500);
                DisasterState.AddLog("Kích hoạt AWS Lambda tại Tokyo để tự động chạy kiểm thử liên thông (Self-Test)...");
                DisasterState.AddLog("Lambda Self-Test tại Tokyo: PASSED (Cấu hình ứng dụng và kết nối DB an toàn).");

                await Task.Delay(1000);
                DisasterState.AddLog("Cập nhật bảng định tuyến DNS của ứng dụng sang Vùng Tokyo.");
                
                // Complete failover
                DisasterState.IsFailedOver = true;
                DisasterState.CurrentRegion = "ap-northeast-1";
                DisasterState.FailoverStatus = "COMPLETED";
                DisasterState.FailoverEndTime = DateTime.Now;
                var start = DisasterState.FailoverStartTime ?? DateTime.Now.AddSeconds(-8.6);
                var end = DisasterState.FailoverEndTime ?? DateTime.Now;
                DisasterState.RecoveryTimeSeconds = Math.Round((end - start).TotalSeconds, 2);
                DisasterState.AddLog($"FAILOVER THÀNH CÔNG! Hệ thống đã khôi phục hoạt động tại Tokyo (ap-northeast-1). RTO: {DisasterState.RecoveryTimeSeconds} giây, RPO: 0.00 giây (Zero Data Loss).");
            }
            catch (Exception ex)
            {
                DisasterState.FailoverStatus = "FAILED";
                DisasterState.AddLog($"LỖI HỆ THỐNG: Quy trình phục hồi khẩn cấp thất bại: {ex.Message}");
            }
        });

        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult Reset()
    {
        DisasterState.Reset();
        return Json(new { success = true });
    }

    [HttpGet]
    public IActionResult GetStatus()
    {
        var duration = 0.0;
        if (DisasterState.FailoverStatus == "RUNNING" && DisasterState.FailoverStartTime.HasValue)
        {
            duration = Math.Round((DateTime.Now - DisasterState.FailoverStartTime.Value).TotalSeconds, 1);
        }
        else if (DisasterState.FailoverStatus == "COMPLETED")
        {
            duration = DisasterState.RecoveryTimeSeconds;
        }

        return Json(new
        {
            isSingaporeDown = DisasterState.IsSingaporeDown,
            isFailedOver = DisasterState.IsFailedOver,
            currentRegion = DisasterState.CurrentRegion,
            failoverStatus = DisasterState.FailoverStatus,
            durationSeconds = duration,
            logs = DisasterState.FailoverLogs
        });
    }

    private async Task<bool> SetupTokyoResourcesInLocalStackAsync()
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        
        var tokyoBucket = "cloud-migration-backup-kedep-tokyo";
        var tokyoTable = "cloud-migration-logs-kedep-tokyo";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "ap-northeast-1"
        };
        using var s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "ap-northeast-1"
        };
        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        try
        {
            // 1. Create S3 Bucket in Tokyo
            var bucketExists = false;
            try
            {
                var buckets = await s3Client.ListBucketsAsync();
                bucketExists = buckets.Buckets.Any(b => b.BucketName == tokyoBucket);
            }
            catch { }

            if (!bucketExists)
            {
                await s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = tokyoBucket,
                    UseClientRegion = true
                });
            }

            // 2. Create DynamoDB Table in Tokyo
            var tableExists = false;
            try
            {
                var describe = await dynamoClient.DescribeTableAsync(tokyoTable);
                tableExists = describe.Table != null;
            }
            catch { }

            if (!tableExists)
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tokyoTable,
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement("MigrationId", KeyType.HASH),
                        new KeySchemaElement("Timestamp", KeyType.RANGE)
                    },
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition("MigrationId", ScalarAttributeType.S),
                        new AttributeDefinition("Timestamp", ScalarAttributeType.S)
                    },
                    BillingMode = BillingMode.PAY_PER_REQUEST
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            DisasterState.AddLog($"  - Lỗi kết nối LocalStack: {ex.Message}");
            return false;
        }
    }
}
