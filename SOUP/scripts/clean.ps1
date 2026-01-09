# ============================================================================
# SOUP Clean Script
# ============================================================================
# Usage:
#   .\scripts\clean.ps1                    # Clean all build artifacts
# ============================================================================

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment
$localSDKPath = "D:\CODE\important files\dotnet-sdk-9.0.306-win-x64"
if (Test-Path $localSDKPath) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Clean" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean main project
$binDir = Join-Path $srcDir "bin"
$objDir = Join-Path $srcDir "obj"

if (Test-Path $binDir) {
    Write-Host "Cleaning src\bin..." -ForegroundColor Yellow
    Remove-Item -Path $binDir -Recurse -Force
}
if (Test-Path $objDir) {
    Write-Host "Cleaning src\obj..." -ForegroundColor Yellow
    Remove-Item -Path $objDir -Recurse -Force
}

# Clean tools
$toolProjects = @("ImportDictionary", "InspectExcel", "InspectOrderDb")
foreach ($tool in $toolProjects) {
    $binDir = Join-Path $rootDir "tools\$tool\bin"
    $objDir = Join-Path $rootDir "tools\$tool\obj"
    
    if (Test-Path $binDir) {
        Write-Host "Cleaning tools\$tool\bin..." -ForegroundColor Yellow
        Remove-Item -Path $binDir -Recurse -Force
    }
    if (Test-Path $objDir) {
        Write-Host "Cleaning tools\$tool\obj..." -ForegroundColor Yellow
        Remove-Item -Path $objDir -Recurse -Force
    }
}

# Always clean publish folders
Write-Host ""
Write-Host "Cleaning publish folders..." -ForegroundColor Yellow

$publishDirs = @(
    "publish",
    "publish-framework",
    "publish-portable",
    "publish-selfcontained"
)

foreach ($dir in $publishDirs) {
    $fullPath = Join-Path $rootDir $dir
    if (Test-Path $fullPath) {
        Write-Host "  Removing $dir..." -ForegroundColor Yellow
        Remove-Item -Path $fullPath -Recurse -Force
    }
}

# Clean installer output
Write-Host ""
Write-Host "Cleaning installer output..." -ForegroundColor Yellow

$installerOutput = Join-Path $rootDir "installer\Output"
if (Test-Path $installerOutput) {
    Write-Host "  Removing installer\Output..." -ForegroundColor Yellow
    Remove-Item -Path $installerOutput -Recurse -Force
}

# Clean setup exe files in installer folder
$setupFiles = Get-ChildItem -Path (Join-Path $rootDir "installer") -Filter "*.exe" -ErrorAction SilentlyContinue
foreach ($file in $setupFiles) {
    Write-Host "  Removing $($file.Name)..." -ForegroundColor Yellow
    Remove-Item -Path $file.FullName -Force
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Clean Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
