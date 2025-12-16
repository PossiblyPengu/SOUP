# ============================================================================
# SOUP Run Script
# ============================================================================
# Usage:
#   .\scripts\run.ps1                      # Build and run (Debug)
#   .\scripts\run.ps1 -Release             # Build and run (Release)
#   .\scripts\run.ps1 -NoBuild             # Run without building (uses last build)
# ============================================================================

param(
    [switch]$Release,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP\SOUP.csproj"
$configuration = if ($Release) { "Release" } else { "Debug" }

# Find dotnet
$dotnetPath = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
if (-not (Test-Path $dotnetPath)) {
    $dotnetPath = "dotnet"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Run - $configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($NoBuild) {
    # Just run without building
    $exePath = Join-Path $srcDir "SOUP\bin\$configuration\net8.0-windows\SOUP.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
        Write-Host "Run without -NoBuild to build first." -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "Running SOUP (no build)..." -ForegroundColor Yellow
    & $exePath
} else {
    # Build and run
    Write-Host "Building and running SOUP..." -ForegroundColor Yellow
    & $dotnetPath run --project $projectFile --configuration $configuration
}
