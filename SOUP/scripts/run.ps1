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

# Setup local .NET SDK environment (prefer any installed .NET 10 SDK in the known folder)
$localSdkRoot = "D:\CODE\important files"
$localSDKPath = $null
if (Test-Path $localSdkRoot) {
    $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'dotnet-sdk-10*' } |
        Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue

    if (-not $localSDKPath) {
        # fallback to any dotnet-sdk folder if a 10.x SDK isn't present
        $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'dotnet-sdk*' } |
            Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue
    }
}

if ($localSDKPath -and (Test-Path $localSDKPath)) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"
$configuration = if ($Release) { "Release" } else { "Debug" }

# Find dotnet (check environment variable, then fallback to system dotnet)
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

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
} else {
    # Build and run
    Write-Host "Building and running SOUP..." -ForegroundColor Yellow
    & $dotnetPath run --project $projectFile --configuration $configuration
}
