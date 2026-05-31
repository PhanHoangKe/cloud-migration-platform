# =========================
# Package Lambda source code
# =========================
data "archive_file" "self_test_lambda_zip" {
  type        = "zip"
  source_file = "${path.module}/lambda/self_test.py"
  output_path = "${path.module}/lambda/self_test.zip"
}

# =========================
# Lambda Function: Migration Self-test
# =========================
resource "aws_lambda_function" "migration_self_test" {
  function_name = "${var.project_name}-self-test-${var.student_code}"
  role          = aws_iam_role.lambda_execution_role.arn
  handler       = "self_test.lambda_handler"
  runtime       = "python3.9"

  filename         = data.archive_file.self_test_lambda_zip.output_path
  source_code_hash = data.archive_file.self_test_lambda_zip.output_base64sha256

  environment {
    variables = {
      MIGRATION_BUCKET_NAME = aws_s3_bucket.migration_backup.bucket
      MIGRATION_LOGS_TABLE  = aws_dynamodb_table.migration_logs.name
      AWS_REGION            = var.aws_region
    }
  }

  tags = {
    Name    = "${var.project_name}-self-test-${var.student_code}"
    Project = var.project_name
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_policy_attachment
  ]
}