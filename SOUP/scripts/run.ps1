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
. "$PSScriptRoot\_common.ps1"

$configuration = if ($Release) { "Release" } else { "Debug" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Run - $configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($NoBuild) {
    # Just run without building
    $exePath = Join-Path $srcDir "bin\$configuration\net10.0-windows10.0.19041.0\win-x64\SOUP.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
        Write-Host "Run without -NoBuild to build first." -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "Running SOUP (no build)..." -ForegroundColor Yellow
    & $exePath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    # Build and run
    Write-Host "Building and running SOUP..." -ForegroundColor Yellow
    & $dotnetPath run --project $projectFile --configuration $configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
