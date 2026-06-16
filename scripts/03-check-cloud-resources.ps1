Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "   STEP 3: CHECKING DEPLOYED CLOUD RESOURCES..." -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$endpoint = "http://localhost:4566"

Write-Host "1. Querying AWS S3 Buckets in LocalStack:" -ForegroundColor Yellow
aws --endpoint-url=$endpoint s3 ls
Write-Host ""

$bucketName = "cloud-migration-backup-vinhuni"
Write-Host "2. Listing Objects in S3 Bucket: $bucketName:" -ForegroundColor Yellow
aws --endpoint-url=$endpoint s3 ls s3://$bucketName --recursive
Write-Host ""

Write-Host "3. Querying AWS DynamoDB Tables in LocalStack:" -ForegroundColor Yellow
aws --endpoint-url=$endpoint dynamodb list-tables
Write-Host ""

$tableName = "cloud-migration-logs-vinhuni"
Write-Host "4. Scanning DynamoDB Table: $tableName (Max 5 entries):" -ForegroundColor Yellow
aws --endpoint-url=$endpoint dynamodb scan --table-name $tableName --max-items 5
Write-Host ""

Write-Host "5. Querying AWS Lambda Functions in LocalStack:" -ForegroundColor Yellow
aws --endpoint-url=$endpoint lambda list-functions --query "Functions[*].[FunctionName,Runtime,Handler]" --output table
Write-Host ""

$lambdaName = "cloud-migration-self-test-vinhuni"
Write-Host "6. Invoking Lambda Function: $lambdaName..." -ForegroundColor Yellow
$responseFile = "response.json"
if (Test-Path $responseFile) { Remove-Item $responseFile -Force }

# Invoke the function (handles both AWS CLI v1 and v2)
aws --endpoint-url=$endpoint lambda invoke --function-name $lambdaName --payload "{}" --cli-binary-format raw-in-base64-out $responseFile

if ($LASTEXITCODE -eq 0 -and (Test-Path $responseFile)) {
    Write-Host "Lambda Response Output:" -ForegroundColor Green
    Get-Content -Path $responseFile
    Remove-Item -Path $responseFile -Force
} else {
    Write-Host "[WARNING] Lambda invocation did not output a valid response.json." -ForegroundColor Red
}

Write-Host "===================================================" -ForegroundColor Cyan
