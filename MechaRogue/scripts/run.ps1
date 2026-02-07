# ============================================================================
# MechaRogue Run Script
# ============================================================================
# Usage:
#   .\scripts\run.ps1                      # Build and run (Debug)
#   .\scripts\run.ps1 -Release             # Build and run (Release)
#   .\scripts\run.ps1 -NoBuild             # Run without building
# ============================================================================

param(
    [switch]$Release,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment (must match global.json: 10.0.101)
$localSDKPath = "D:\CODE\important files\dotnet-sdk-10.0.101-win-x64"

if ($localSDKPath -and (Test-Path $localSDKPath)) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$projectFile = Join-Path $rootDir "MechaRogue.csproj"
$configuration = if ($Release) { "Release" } else { "Debug" }
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MechaRogue - $configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($NoBuild) {
    $exePath = Join-Path $rootDir "bin\$configuration\net10.0-windows\MechaRogue.exe"

    if (-not (Test-Path $exePath)) {
        Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
        Write-Host "Run without -NoBuild to build first." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Running MechaRogue (no build)..." -ForegroundColor Yellow
    & $exePath
} else {
    Write-Host "Building and running MechaRogue..." -ForegroundColor Yellow
    & $dotnetPath run --project $projectFile --configuration $configuration
}
