$projectRoot = "D:\cloud-migration-platform"
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "   STEP 1: STARTING LOCALSTACK SIMULATOR..." -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

Write-Host "Moving to project root: $projectRoot" -ForegroundColor Gray
Set-Location -Path $projectRoot

Write-Host "Running: docker compose up -d" -ForegroundColor Yellow
docker compose up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to run docker compose up!" -ForegroundColor Red
    exit 1
}

Write-Host "Waiting 8 seconds for LocalStack services to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 8

Write-Host "Checking Docker container status:" -ForegroundColor Green
docker ps

Write-Host "Verifying LocalStack AWS S3 service connectivity:" -ForegroundColor Green
aws --endpoint-url=http://localhost:4566 s3 ls
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARNING] LocalStack S3 is not responding yet. It may need more time to initialize." -ForegroundColor Yellow
} else {
    Write-Host "[SUCCESS] LocalStack started successfully and S3 endpoint is online!" -ForegroundColor Green
}

Write-Host "===================================================" -ForegroundColor Cyan
