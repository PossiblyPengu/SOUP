#!/usr/bin/env pwsh
# Test launcher functionality

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Testing Standalone App Paths" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Simulate the launcher's path calculation
$launcherExe = "BusinessToolsSuite\src\BusinessToolsSuite.Desktop\bin\Debug\net8.0\BusinessToolsSuite.Desktop.exe"
$launcherDir = Split-Path -Parent $launcherExe
$baseDir = [System.IO.Path]::GetFullPath((Join-Path $launcherDir ".." ".." ".." ".." ".." ".."))

Write-Host "Simulated launcher location: $launcherExe" -ForegroundColor Yellow
Write-Host "Calculated base directory: $baseDir`n" -ForegroundColor Yellow

$apps = @(
    @{Name="AllocationBuddy"; Dir="AllocationBuddyApp"},
    @{Name="EssentialsBuddy"; Dir="EssentialsBuddyApp"},
    @{Name="ExpireWise"; Dir="ExpireWiseApp"}
)

foreach ($app in $apps) {
    $appPath = Join-Path $baseDir "$($app.Dir)\src\BusinessToolsSuite.Desktop\bin\Debug\net8.0\BusinessToolsSuite.Desktop.exe"

    if (Test-Path $appPath) {
        Write-Host "✓ $($app.Name) FOUND" -ForegroundColor Green
        Write-Host "  Path: $appPath" -ForegroundColor Gray

        # Check file size
        $fileInfo = Get-Item $appPath
        Write-Host "  Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
        Write-Host "  Modified: $($fileInfo.LastWriteTime)" -ForegroundColor Gray
    } else {
        Write-Host "✗ $($app.Name) NOT FOUND" -ForegroundColor Red
        Write-Host "  Expected at: $appPath" -ForegroundColor Gray
    }
    Write-Host ""
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Testing Direct Launch" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$testApp = Join-Path $baseDir "AllocationBuddyApp\src\BusinessToolsSuite.Desktop\bin\Debug\net8.0\BusinessToolsSuite.Desktop.exe"

if (Test-Path $testApp) {
    Write-Host "Attempting to launch AllocationBuddy..." -ForegroundColor Yellow
    try {
        $process = Start-Process -FilePath $testApp -PassThru
        Start-Sleep -Seconds 2

        if ($process.HasExited) {
            Write-Host "✗ App launched but immediately exited (Exit Code: $($process.ExitCode))" -ForegroundColor Red
            Write-Host "  Check logs at: %AppData%\AllocationBuddy\Logs" -ForegroundColor Yellow
        } else {
            Write-Host "✓ App is running (Process ID: $($process.Id))" -ForegroundColor Green
            Write-Host "  Closing test app..." -ForegroundColor Gray
            Stop-Process -Id $process.Id -Force
        }
    } catch {
        Write-Host "✗ Failed to launch: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Cannot test - AllocationBuddy executable not found" -ForegroundColor Red
}

Write-Host "`n"
