#!/usr/bin/env pwsh
# Run ExpireWise standalone app

$ErrorActionPreference = "Stop"

Write-Host "Building and running ExpireWise..." -ForegroundColor Cyan

# Build the app
Write-Host "`nBuilding ExpireWise..." -ForegroundColor Yellow
& dotnet build ExpireWiseApp/ExpireWiseApp.sln --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nLaunching ExpireWise..." -ForegroundColor Green
& dotnet run --project ExpireWiseApp/src/BusinessToolsSuite.Desktop/BusinessToolsSuite.Desktop.csproj --no-build
