# ============================================================================
# SOUP Build Script
# ============================================================================
# Usage:
#   .\scripts\build.ps1                    # Debug build
#   .\scripts\build.ps1 -Release           # Release build
#   .\scripts\build.ps1 -Clean             # Clean before building
#   .\scripts\build.ps1 -Restore           # Restore packages before building
# ============================================================================

param(
    [switch]$Release,
    [switch]$Clean,
    [switch]$Restore,
    [switch]$Verbose
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
Write-Host "  SOUP Build - $configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "[Clean] Removing build artifacts..." -ForegroundColor Yellow
    $binDir = Join-Path $srcDir "bin"
    $objDir = Join-Path $srcDir "obj"
    
    if (Test-Path $binDir) { Remove-Item -Path $binDir -Recurse -Force }
    if (Test-Path $objDir) { Remove-Item -Path $objDir -Recurse -Force }
    
    Write-Host "  Cleaned!" -ForegroundColor Green
}

# Restore if requested
if ($Restore) {
    Write-Host "[Restore] Restoring NuGet packages..." -ForegroundColor Yellow
    & $dotnetPath restore $projectFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Restore failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Restored!" -ForegroundColor Green
}

# Build
Write-Host "[Build] Building $configuration..." -ForegroundColor Yellow

$verbosity = if ($Verbose) { "normal" } else { "minimal" }

# Check if restore is needed (assets file missing)
$assetsFile = Join-Path $srcDir "obj\project.assets.json"
$needsRestore = -not (Test-Path $assetsFile)

if ($needsRestore -and -not $Restore) {
    Write-Host "  (Auto-restoring packages...)" -ForegroundColor Gray
}

# Build arguments: include --no-restore only when we explicitly want no restore
$noRestore = -not ($Restore -or $needsRestore)
$buildArgs = @($projectFile, "--configuration", $configuration, "--verbosity", $verbosity)
if ($noRestore) { $buildArgs += "--no-restore" }

& $dotnetPath build @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Succeeded!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$outputDir = Join-Path $srcDir "bin\$configuration\net10.0-windows"
Write-Host "Output: $outputDir" -ForegroundColor White
