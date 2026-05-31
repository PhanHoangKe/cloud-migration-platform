output "project_name" {
  value = var.project_name
}

output "aws_region" {
  value = var.aws_region
}

output "vpc_id" {
  value = aws_vpc.main.id
}

output "public_subnet_id" {
  value = aws_subnet.public.id
}

output "private_subnet_id" {
  value = aws_subnet.private.id
}

output "migration_backup_bucket_name" {
  value = aws_s3_bucket.migration_backup.bucket
}

output "migration_logs_table_name" {
  value = aws_dynamodb_table.migration_logs.name
}

output "lambda_execution_role_arn" {
  value = aws_iam_role.lambda_execution_role.arn
}

output "app_migration_role_arn" {
  value = aws_iam_role.app_migration_role.arn
}

output "self_test_lambda_name" {
  value = aws_lambda_function.migration_self_test.function_name
}