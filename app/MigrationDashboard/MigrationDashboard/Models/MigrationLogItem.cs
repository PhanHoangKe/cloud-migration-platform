using Amazon.DynamoDBv2.DataModel;

namespace MigrationDashboard.Models;

[DynamoDBTable("cloud-migration-logs-vinhuni")]
public class MigrationLogItem
{
    [DynamoDBHashKey]
    public string MigrationId { get; set; } = string.Empty;

    [DynamoDBRangeKey]
    public string Timestamp { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
