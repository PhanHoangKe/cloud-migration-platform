using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;
using MigrationDashboard.Models;

namespace MigrationDashboard.Services;

public class DatabaseExportService : IDatabaseExportService
{
    private readonly IConfiguration _configuration;
    public static string? CustomConnectionString { get; set; }

    public DatabaseExportService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<DatabaseExportResultViewModel> ExportDatabaseAsync(string migrationId)
    {
        var localStackConfig = _configuration.GetSection("LocalStack");
        var serviceUrl = localStackConfig["ServiceUrl"] ?? "http://localhost:4566";
        var region = localStackConfig["Region"] ?? "ap-southeast-1";
        var accessKey = localStackConfig["AccessKey"] ?? "test";
        var secretKey = localStackConfig["SecretKey"] ?? "test";
        var bucketName = localStackConfig["MigrationBucketName"] ?? "cloud-migration-backup-vinhuni";
        var tableName = localStackConfig["MigrationLogsTableName"] ?? "cloud-migration-logs-vinhuni";

        var result = new DatabaseExportResultViewModel
        {
            MigrationId = migrationId,
            ExportedAt = DateTime.UtcNow,
            ConnectionStatus = false,
            TotalTables = 0,
            TotalRows = 0
        };

        // 1. Initialize AWS Clients for LocalStack logging and upload
        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = region
        };
        using var s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region
        };
        using var dynamoClient = new AmazonDynamoDBClient(accessKey, secretKey, dynamoConfig);

        // 2. Read connection string (uses custom configured connection string if set via UI)
        var connectionString = CustomConnectionString ?? _configuration.GetConnectionString("OnPremDatabase");
        if (string.IsNullOrEmpty(connectionString))
        {
            result.ErrorMessage = "Connection string 'OnPremDatabase' is not configured.";
            await LogToDynamoDbAsync(dynamoClient, tableName, migrationId, "DATABASE_EXPORT_FAILED", "Database connection string missing");
            return result;
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            result.ConnectionStatus = true;
            await LogToDynamoDbAsync(dynamoClient, tableName, migrationId, "DATABASE_CONNECTED", "Database connected successfully");

            // 3. Get user tables list
            var tablesList = new List<(string Schema, string Name)>();
            using (var cmd = new SqlCommand("SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string schema = reader.GetString(0);
                    string name = reader.GetString(1);
                    if (name.Equals("sysdiagrams", StringComparison.OrdinalIgnoreCase)) continue;
                    tablesList.Add((schema, name));
                }
            }

            result.TotalTables = tablesList.Count;

            // 4. Export columns, counts, and samples for each table
            foreach (var tableInfo in tablesList)
            {
                var tableExport = new DatabaseTableExportViewModel
                {
                    SchemaName = tableInfo.Schema,
                    TableName = tableInfo.Name
                };

                try
                {
                    // A. Fetch column metadata
                    using (var colCmd = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table ORDER BY ORDINAL_POSITION", connection))
                    {
                        colCmd.Parameters.AddWithValue("@Schema", tableInfo.Schema);
                        colCmd.Parameters.AddWithValue("@Table", tableInfo.Name);

                        using var colReader = await colCmd.ExecuteReaderAsync();
                        while (await colReader.ReadAsync())
                        {
                            tableExport.Columns.Add(new DatabaseColumnViewModel
                            {
                                ColumnName = colReader.GetString(0),
                                DataType = colReader.GetString(1),
                                IsNullable = colReader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase)
                            });
                        }
                    }

                    // B. Count rows
                    using (var countCmd = new SqlCommand($"SELECT COUNT(*) FROM [{tableInfo.Schema}].[{tableInfo.Name}]", connection))
                    {
                        var scalarResult = await countCmd.ExecuteScalarAsync();
                        var rowCount = scalarResult != null && scalarResult != DBNull.Value ? Convert.ToInt32(scalarResult) : 0;
                        tableExport.RowCount = rowCount;
                        result.TotalRows += rowCount;
                    }

                    // C. Get sample rows (Top 20)
                    using (var sampleCmd = new SqlCommand($"SELECT TOP 20 * FROM [{tableInfo.Schema}].[{tableInfo.Name}]", connection))
                    {
                        using var sampleReader = await sampleCmd.ExecuteReaderAsync();
                        while (await sampleReader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < sampleReader.FieldCount; i++)
                            {
                                string colName = sampleReader.GetName(i);
                                object value = sampleReader.GetValue(i);

                                if (value == DBNull.Value)
                                {
                                    row[colName] = null!;
                                }
                                else if (value is byte[] bytes)
                                {
                                    row[colName] = Convert.ToBase64String(bytes);
                                }
                                else
                                {
                                    row[colName] = value;
                                }
                            }
                            tableExport.SampleRows.Add(row);
                        }
                    }

                    result.Tables.Add(tableExport);
                }
                catch (Exception ex)
                {
                    // Log table-specific export warning but do not fail the entire process
                    tableExport.RowCount = -1;
                    tableExport.SampleRows.Add(new Dictionary<string, object> { { "Warning", $"Failed to read table data: {ex.Message}" } });
                    result.Tables.Add(tableExport);
                }
            }

            // 5. Serialize to database-export.json
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            
            // Create local packages directory
            var localDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "migration-packages", migrationId);
            if (!Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            var localFilePath = Path.Combine(localDir, "database-export.json");
            await File.WriteAllTextAsync(localFilePath, json);

            await LogToDynamoDbAsync(dynamoClient, tableName, migrationId, "DATABASE_EXPORTED", "Database exported to JSON successfully");

            // 6. Upload file to S3
            var s3Key = $"migrations/{migrationId}/database-export.json";
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                FilePath = localFilePath,
                ContentType = "application/json"
            };
            await s3Client.PutObjectAsync(putRequest);

            result.S3Key = s3Key;
            await LogToDynamoDbAsync(dynamoClient, tableName, migrationId, "DATABASE_EXPORT_UPLOADED_TO_S3", "Database export uploaded to S3 successfully");
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            await LogToDynamoDbAsync(dynamoClient, tableName, migrationId, "DATABASE_EXPORT_FAILED", $"Database export failed: {ex.Message}");
        }

        return result;
    }

    private async Task LogToDynamoDbAsync(IAmazonDynamoDB client, string tableName, string migrationId, string status, string message)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "MigrationId", new AttributeValue { S = migrationId } },
                { "Timestamp", new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") } },
                { "Status", new AttributeValue { S = status } },
                { "Message", new AttributeValue { S = message } },
                { "Step", new AttributeValue { S = "DATABASE_EXPORT" } }
            };

            var request = new PutItemRequest
            {
                TableName = tableName,
                Item = item
            };

            await client.PutItemAsync(request);
        }
        catch
        {
            // Suppress logging failures to avoid crash
        }
    }
}
