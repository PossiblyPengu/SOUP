<#
.SYNOPSIS
    Builds and runs the EssentialsBuddy standalone application.

.DESCRIPTION
    This script builds and launches the EssentialsBuddy standalone application
    using the local .NET SDK.

.NOTES
    Author: PossiblyPengu
    Version: 1.0.0
#>

param(
    [switch]$NoBuild,
    [switch]$Release
)

# ============================================
# Configuration
# ============================================

$ErrorActionPreference = "Stop"

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Split-Path -Parent $scriptPath
Set-Location $rootPath

$dotnet = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
$project = "SAP\src\EssentialsBuddy.Standalone\EssentialsBuddy.Standalone.csproj"
$configuration = if ($Release) { "Release" } else { "Debug" }

# ============================================
# Banner
# ============================================

Write-Host ""
Write-Host "  ╔═══════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║         EssentialsBuddy Launcher          ║" -ForegroundColor Cyan
Write-Host "  ╚═══════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ============================================
# Validation
# ============================================

if (-not (Test-Path $dotnet)) {
    Write-Host "ERROR: .NET SDK not found at: $dotnet" -ForegroundColor Red
    Write-Host "Please update the script with the correct SDK path." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

if (-not (Test-Path $project)) {
    Write-Host "ERROR: Project not found at: $project" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# ============================================
# Build
# ============================================

if (-not $NoBuild) {
    Write-Host "Building project ($configuration)..." -ForegroundColor Yellow
    & "$dotnet" build $project --configuration $configuration
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build failed!" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
}

# ============================================
# Run
# ============================================

Write-Host ""
Write-Host "Starting EssentialsBuddy..." -ForegroundColor Cyan
Write-Host ""

$exePath = "SAP\src\EssentialsBuddy.Standalone\bin\$configuration\net8.0-windows\EssentialsBuddy.exe"
if (Test-Path $exePath) {
    & $exePath
} else {
    # Fall back to dotnet run
    & "$dotnet" run --project $project --configuration $configuration --no-build
}
