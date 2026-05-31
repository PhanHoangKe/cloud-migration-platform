# =========================
# S3 Bucket for Migration Backup
# =========================
resource "aws_s3_bucket" "migration_backup" {
  bucket = "${var.project_name}-backup-${var.student_code}"

  tags = {
    Name        = "${var.project_name}-backup-${var.student_code}"
    Project     = var.project_name
    Environment = "localstack"
    Purpose     = "migration-backup"
  }
}

# =========================
# S3 Bucket Versioning
# =========================
resource "aws_s3_bucket_versioning" "migration_backup_versioning" {
  bucket = aws_s3_bucket.migration_backup.id

  versioning_configuration {
    status = "Enabled"
  }
}

# =========================
# Server-side encryption
# =========================
resource "aws_s3_bucket_server_side_encryption_configuration" "migration_backup_encryption" {
  bucket = aws_s3_bucket.migration_backup.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# =========================
# Block public access
# =========================
resource "aws_s3_bucket_public_access_block" "migration_backup_public_access" {
  bucket = aws_s3_bucket.migration_backup.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}