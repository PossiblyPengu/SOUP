#!/usr/bin/env pwsh
# Run AllocationBuddy standalone app

$ErrorActionPreference = "Stop"

Write-Host "Building and running AllocationBuddy..." -ForegroundColor Cyan

# Build the app
Write-Host "`nBuilding AllocationBuddy..." -ForegroundColor Yellow
& dotnet build AllocationBuddyApp/AllocationBuddyApp.sln --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nLaunching AllocationBuddy..." -ForegroundColor Green
& dotnet run --project AllocationBuddyApp/src/BusinessToolsSuite.Desktop/BusinessToolsSuite.Desktop.csproj --no-build
