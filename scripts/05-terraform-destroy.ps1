$terraformDir = "D:\cloud-migration-platform\terraform"
$endpoint = "http://localhost:4566"

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "   STEP 5: RUNNING TERRAFORM DESTROY CLEANUP..." -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

Write-Host "Moving to Terraform directory: $terraformDir" -ForegroundColor Gray
Set-Location -Path $terraformDir

Write-Host "Running: terraform destroy -auto-approve" -ForegroundColor Yellow
terraform destroy -auto-approve
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Terraform destroy command failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Verifying resources were successfully teardown:" -ForegroundColor Yellow

Write-Host "1. Querying AWS S3 Buckets in LocalStack:" -ForegroundColor Green
aws --endpoint-url=$endpoint s3 ls

Write-Host "2. Querying AWS DynamoDB Tables in LocalStack:" -ForegroundColor Green
aws --endpoint-url=$endpoint dynamodb list-tables

Write-Host "3. Querying AWS Lambda Functions in LocalStack:" -ForegroundColor Green
aws --endpoint-url=$endpoint lambda list-functions --query "Functions[*].[FunctionName,Runtime]" --output table

Write-Host ""
Write-Host "[SUCCESS] Cleanup completed successfully! All simulated cloud resources have been destroyed." -ForegroundColor Green
Write-Host "===================================================" -ForegroundColor Cyan
