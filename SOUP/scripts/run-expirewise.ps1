# ============================================================================
# SOUP Run ExpireWise Module
# ============================================================================
# Usage:
#   .\scripts\run-expirewise.ps1              # Build and run ExpireWise
#   .\scripts\run-expirewise.ps1 -NoBuild     # Run without building
# ============================================================================

param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment (prefer any installed .NET 10 SDK)
$localSdkRoot = "D:\CODE\important files"
$localSDKPath = $null
if (Test-Path $localSdkRoot) {
    $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'dotnet-sdk-10*' } |
        Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue

    if (-not $localSDKPath) {
        $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'dotnet-sdk*' } |
            Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue
    }
}

if ($localSDKPath -and (Test-Path $localSDKPath)) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"
$configuration = "Debug"
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  SOUP - ExpireWise Module" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

if ($NoBuild) {
    $exePath = Join-Path $srcDir "bin\$configuration\net10.0-windows10.0.19041.0\win-x64\SOUP.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
        Write-Host "Run without -NoBuild to build first." -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "Running ExpireWise (no build)..." -ForegroundColor Yellow
    & $exePath --module expirewise --no-widget
} else {
    Write-Host "Building and running ExpireWise..." -ForegroundColor Yellow
    & $dotnetPath run --project $projectFile --configuration $configuration -- --module expirewise --no-widget
}
