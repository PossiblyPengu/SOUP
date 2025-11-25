#!/usr/bin/env pwsh
# Build all applications

$ErrorActionPreference = "Stop"

Write-Host "Building all Business Tools Suite applications..." -ForegroundColor Cyan

$solutions = @(
    @{Name="Business Tools Suite Launcher"; Path="BusinessToolsSuite/BusinessToolsSuite.sln"},
    @{Name="AllocationBuddy"; Path="AllocationBuddyApp/AllocationBuddyApp.sln"},
    @{Name="EssentialsBuddy"; Path="EssentialsBuddyApp/EssentialsBuddyApp.sln"},
    @{Name="ExpireWise"; Path="ExpireWiseApp/ExpireWiseApp.sln"}
)

$failed = @()
$succeeded = @()

foreach ($solution in $solutions) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Building $($solution.Name)..." -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Cyan

    & dotnet build $solution.Path --nologo

    if ($LASTEXITCODE -eq 0) {
        $succeeded += $solution.Name
        Write-Host "✓ $($solution.Name) build succeeded" -ForegroundColor Green
    } else {
        $failed += $solution.Name
        Write-Host "✗ $($solution.Name) build failed" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($succeeded.Count -gt 0) {
    Write-Host "`nSucceeded ($($succeeded.Count)):" -ForegroundColor Green
    foreach ($name in $succeeded) {
        Write-Host "  ✓ $name" -ForegroundColor Green
    }
}

if ($failed.Count -gt 0) {
    Write-Host "`nFailed ($($failed.Count)):" -ForegroundColor Red
    foreach ($name in $failed) {
        Write-Host "  ✗ $name" -ForegroundColor Red
    }
    exit 1
}

Write-Host "`nAll applications built successfully! 🎉" -ForegroundColor Green
