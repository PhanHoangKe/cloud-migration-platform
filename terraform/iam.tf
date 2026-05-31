# =========================
# IAM Role for Lambda Self-test
# =========================
resource "aws_iam_role" "lambda_execution_role" {
  name = "${var.project_name}-lambda-role-${var.student_code}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })

  tags = {
    Name    = "${var.project_name}-lambda-role-${var.student_code}"
    Project = var.project_name
  }
}

# =========================
# IAM Policy for Lambda
# Allows Lambda to read S3 and write/read DynamoDB logs
# =========================
resource "aws_iam_policy" "lambda_migration_policy" {
  name        = "${var.project_name}-lambda-policy-${var.student_code}"
  description = "Least privilege policy for migration self-test Lambda"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowReadMigrationBackupBucket"
        Effect = "Allow"
        Action = [
          "s3:ListBucket"
        ]
        Resource = aws_s3_bucket.migration_backup.arn
      },
      {
        Sid    = "AllowReadMigrationBackupObjects"
        Effect = "Allow"
        Action = [
          "s3:GetObject"
        ]
        Resource = "${aws_s3_bucket.migration_backup.arn}/*"
      },
      {
        Sid    = "AllowReadWriteMigrationLogs"
        Effect = "Allow"
        Action = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:Scan",
          "dynamodb:Query"
        ]
        Resource = aws_dynamodb_table.migration_logs.arn
      },
      {
        Sid    = "AllowWriteCloudWatchLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "*"
      }
    ]
  })

  tags = {
    Name    = "${var.project_name}-lambda-policy-${var.student_code}"
    Project = var.project_name
  }
}

# =========================
# Attach Policy to Lambda Role
# =========================
resource "aws_iam_role_policy_attachment" "lambda_policy_attachment" {
  role       = aws_iam_role.lambda_execution_role.name
  policy_arn = aws_iam_policy.lambda_migration_policy.arn
}

# =========================
# IAM Role for Application Simulation
# This role represents the ASP.NET Core Migration Dashboard
# =========================
resource "aws_iam_role" "app_migration_role" {
  name = "${var.project_name}-app-role-${var.student_code}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
      }
    ]
  })

  tags = {
    Name    = "${var.project_name}-app-role-${var.student_code}"
    Project = var.project_name
  }
}

# =========================
# IAM Policy for ASP.NET Core App
# Allows app to upload backup to S3 and write migration logs
# =========================
resource "aws_iam_policy" "app_migration_policy" {
  name        = "${var.project_name}-app-policy-${var.student_code}"
  description = "Least privilege policy for ASP.NET Core migration dashboard"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowUploadMigrationBackup"
        Effect = "Allow"
        Action = [
          "s3:PutObject",
          "s3:GetObject",
          "s3:ListBucket"
        ]
        Resource = [
          aws_s3_bucket.migration_backup.arn,
          "${aws_s3_bucket.migration_backup.arn}/*"
        ]
      },
      {
        Sid    = "AllowWriteMigrationLogs"
        Effect = "Allow"
        Action = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:Scan",
          "dynamodb:Query"
        ]
        Resource = aws_dynamodb_table.migration_logs.arn
      }
    ]
  })

  tags = {
    Name    = "${var.project_name}-app-policy-${var.student_code}"
    Project = var.project_name
  }
}

# =========================
# Attach Policy to App Role
# =========================
resource "aws_iam_role_policy_attachment" "app_policy_attachment" {
  role       = aws_iam_role.app_migration_role.name
  policy_arn = aws_iam_policy.app_migration_policy.arn
}