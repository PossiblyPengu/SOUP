# ============================================================================
# SOUP Watch Script (Hot Reload)
# ============================================================================
# Usage:
#   .\scripts\watch.ps1                    # Run with hot reload
# ============================================================================

$ErrorActionPreference = "Stop"

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP\SOUP.csproj"

# Find dotnet
$dotnetPath = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
if (-not (Test-Path $dotnetPath)) {
    $dotnetPath = "dotnet"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Watch (Hot Reload)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting with hot reload..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop." -ForegroundColor Gray
Write-Host ""

& $dotnetPath watch run --project $projectFile
