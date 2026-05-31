using System;
using System.Threading.Tasks;

namespace MigrationDashboard.Services;

public interface IMigrationReportService
{
    Task<(string jsonPath, string htmlPath)> GenerateReportFilesAsync(
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
        string tempDirectory
    );
}
