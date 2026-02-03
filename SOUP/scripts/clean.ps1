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

# Kill any running SOUP processes first to release file locks
$soupProcesses = Get-Process -Name "SOUP" -ErrorAction SilentlyContinue
if ($soupProcesses) {
    Write-Host "Stopping running SOUP processes..." -ForegroundColor Yellow
    $soupProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

# Also kill any dotnet processes that might be holding locks (e.g., hot reload)
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.MainWindowTitle -like "*SOUP*" -or $_.CommandLine -like "*SOUP*"
}
if ($dotnetProcesses) {
    Write-Host "Stopping dotnet processes..." -ForegroundColor Yellow
    $dotnetProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# Helper function to remove directory with retry
function Remove-DirectoryWithRetry {
    param([string]$Path, [int]$MaxRetries = 3)
    
    if (-not (Test-Path $Path)) { return }
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($i -eq $MaxRetries) {
                Write-Host "  Warning: Could not fully remove $Path - some files may be locked" -ForegroundColor Red
            }
            else {
                Write-Host "  Retry $i/$MaxRetries for $Path..." -ForegroundColor DarkYellow
                Start-Sleep -Milliseconds 500
            }
        }
    }
}

# Clean main project
$binDir = Join-Path $srcDir "bin"
$objDir = Join-Path $srcDir "obj"

if (Test-Path $binDir) {
    Write-Host "Cleaning src\bin..." -ForegroundColor Yellow
    Remove-DirectoryWithRetry -Path $binDir
}
if (Test-Path $objDir) {
    Write-Host "Cleaning src\obj..." -ForegroundColor Yellow
    Remove-DirectoryWithRetry -Path $objDir
}

# Clean tools
$toolProjects = @("ImportDictionary", "InspectExcel", "InspectOrderDb")
foreach ($tool in $toolProjects) {
    $binDir = Join-Path $rootDir "tools\$tool\bin"
    $objDir = Join-Path $rootDir "tools\$tool\obj"
    
    if (Test-Path $binDir) {
        Write-Host "Cleaning tools\$tool\bin..." -ForegroundColor Yellow
        Remove-DirectoryWithRetry -Path $binDir
    }
    if (Test-Path $objDir) {
        Write-Host "Cleaning tools\$tool\obj..." -ForegroundColor Yellow
        Remove-DirectoryWithRetry -Path $objDir
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
        Remove-DirectoryWithRetry -Path $fullPath
    }
}

# Clean installer output
Write-Host ""
Write-Host "Cleaning installer output..." -ForegroundColor Yellow

$installerOutput = Join-Path $rootDir "installer\Output"
if (Test-Path $installerOutput) {
    Write-Host "  Removing installer\Output..." -ForegroundColor Yellow
    Remove-DirectoryWithRetry -Path $installerOutput
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
