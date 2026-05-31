$terraformDir = "D:\cloud-migration-platform\terraform"
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "   STEP 2: RUNNING TERRAFORM INFRASTRUCTURE IAAC..." -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

Write-Host "Moving to Terraform directory: $terraformDir" -ForegroundColor Gray
Set-Location -Path $terraformDir

Write-Host "1. Running 'terraform init'..." -ForegroundColor Yellow
terraform init
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Terraform initialization failed!" -ForegroundColor Red
    exit 1
}

Write-Host "2. Running 'terraform fmt'..." -ForegroundColor Yellow
terraform fmt
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARNING] Format command returned warnings." -ForegroundColor Yellow
}

Write-Host "3. Running 'terraform validate'..." -ForegroundColor Yellow
terraform validate
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Terraform syntax validation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "4. Running 'terraform apply -auto-approve'..." -ForegroundColor Yellow
terraform apply -auto-approve
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Terraform deployment failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[SUCCESS] Terraform applied successfully! Simulated cloud resources are ready in LocalStack." -ForegroundColor Green
Write-Host "===================================================" -ForegroundColor Cyan
