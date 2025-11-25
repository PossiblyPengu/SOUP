#!/usr/bin/env pwsh
# Run EssentialsBuddy standalone app

$ErrorActionPreference = "Stop"

Write-Host "Building and running EssentialsBuddy..." -ForegroundColor Cyan

# Build the app
Write-Host "`nBuilding EssentialsBuddy..." -ForegroundColor Yellow
& dotnet build EssentialsBuddyApp/EssentialsBuddyApp.sln --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nLaunching EssentialsBuddy..." -ForegroundColor Green
& dotnet run --project EssentialsBuddyApp/src/BusinessToolsSuite.Desktop/BusinessToolsSuite.Desktop.csproj --no-build
