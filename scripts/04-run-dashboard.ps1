$dashboardDir = "D:\cloud-migration-platform\app\MigrationDashboard\MigrationDashboard"
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "   STEP 4: RUNNING ASP.NET CORE 9 DASHBOARD..." -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

Write-Host "Moving to dashboard project directory: $dashboardDir" -ForegroundColor Gray
Set-Location -Path $dashboardDir

Write-Host "1. Building the project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Project compilation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "2. Launching web application server..." -ForegroundColor Yellow
Write-Host ">> Access Cloud Migration Dashboard at: http://localhost:5097" -ForegroundColor Green
Write-Host ">> Press Ctrl+C to terminate the application." -ForegroundColor Yellow
dotnet run --launch-profile "http"
