#!/usr/bin/env pwsh
# Run the Business Tools Suite Launcher

$ErrorActionPreference = "Stop"

Write-Host "Building and running Business Tools Suite Launcher..." -ForegroundColor Cyan

# Build the launcher
Write-Host "`nBuilding launcher..." -ForegroundColor Yellow
& dotnet build BusinessToolsSuite/BusinessToolsSuite.sln --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nLaunching Business Tools Suite..." -ForegroundColor Green
& dotnet run --project BusinessToolsSuite/src/BusinessToolsSuite.Desktop/BusinessToolsSuite.Desktop.csproj --no-build
