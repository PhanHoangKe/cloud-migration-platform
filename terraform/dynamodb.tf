# =========================
# DynamoDB Table for Migration Logs
# =========================
resource "aws_dynamodb_table" "migration_logs" {
  name         = "${var.project_name}-logs-${var.student_code}"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "MigrationId"
  range_key    = "Timestamp"

  attribute {
    name = "MigrationId"
    type = "S"
  }

  attribute {
    name = "Timestamp"
    type = "S"
  }

  tags = {
    Name        = "${var.project_name}-logs-${var.student_code}"
    Project     = var.project_name
    Environment = "localstack"
    Purpose     = "migration-logs"
  }
}