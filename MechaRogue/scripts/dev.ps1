# ============================================================================
# MechaRogue Quick Commands
# ============================================================================
# Usage:
#   .\scripts\dev.ps1 build                # Quick build (debug)
#   .\scripts\dev.ps1 run                  # Build and run
#   .\scripts\dev.ps1 watch                # Hot reload mode
#   .\scripts\dev.ps1 clean                # Clean build artifacts
#   .\scripts\dev.ps1 rebuild              # Clean + build
#   .\scripts\dev.ps1 restore              # Restore NuGet packages
#   .\scripts\dev.ps1 check                # Build with warnings as errors
#   .\scripts\dev.ps1 info                 # Show project info
# ============================================================================

param(
    [Parameter(Position=0)]
    [ValidateSet("build", "run", "watch", "clean", "rebuild", "restore", "check", "info", "help")]
    [string]$Command = "help"
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
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

function Show-Header($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Show-Help {
    Write-Host ""
    Write-Host "MechaRogue Dev Commands" -ForegroundColor Cyan
    Write-Host "=======================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  build     " -NoNewline -ForegroundColor Yellow; Write-Host "Quick debug build"
    Write-Host "  run       " -NoNewline -ForegroundColor Yellow; Write-Host "Build and run game"
    Write-Host "  watch     " -NoNewline -ForegroundColor Yellow; Write-Host "Hot reload development"
    Write-Host "  clean     " -NoNewline -ForegroundColor Yellow; Write-Host "Remove build artifacts"
    Write-Host "  rebuild   " -NoNewline -ForegroundColor Yellow; Write-Host "Clean + build"
    Write-Host "  restore   " -NoNewline -ForegroundColor Yellow; Write-Host "Restore NuGet packages"
    Write-Host "  check     " -NoNewline -ForegroundColor Yellow; Write-Host "Build with warnings as errors"
    Write-Host "  info      " -NoNewline -ForegroundColor Yellow; Write-Host "Show project info"
    Write-Host ""
}

switch ($Command) {
    "build" {
        Show-Header "Building (Debug)"
        & $dotnetPath build $projectFile --configuration Debug --no-restore
    }
    "run" {
        Show-Header "Running MechaRogue"
        & $dotnetPath run --project $projectFile --configuration Debug
    }
    "watch" {
        Show-Header "Hot Reload Mode"
        & $dotnetPath watch run --project $projectFile
    }
    "clean" {
        Show-Header "Cleaning"
        $binDir = Join-Path $rootDir "bin"
        $objDir = Join-Path $rootDir "obj"
        if (Test-Path $binDir) { Remove-Item -Path $binDir -Recurse -Force; Write-Host "  Removed bin/" -ForegroundColor Gray }
        if (Test-Path $objDir) { Remove-Item -Path $objDir -Recurse -Force; Write-Host "  Removed obj/" -ForegroundColor Gray }
        Write-Host "Done!" -ForegroundColor Green
    }
    "rebuild" {
        Show-Header "Rebuilding"
        $binDir = Join-Path $rootDir "bin"
        $objDir = Join-Path $rootDir "obj"
        if (Test-Path $binDir) { Remove-Item -Path $binDir -Recurse -Force }
        if (Test-Path $objDir) { Remove-Item -Path $objDir -Recurse -Force }
        & $dotnetPath build $projectFile --configuration Debug
    }
    "restore" {
        Show-Header "Restoring Packages"
        & $dotnetPath restore $projectFile
    }
    "check" {
        Show-Header "Building with Warnings as Errors"
        & $dotnetPath build $projectFile --configuration Debug -warnaserror
    }
    "info" {
        Show-Header "Project Info"
        Write-Host ""
        Write-Host "  Project:  " -NoNewline -ForegroundColor Gray; Write-Host "MechaRogue"
        Write-Host "  Root:     " -NoNewline -ForegroundColor Gray; Write-Host $rootDir
        Write-Host "  .NET:     " -NoNewline -ForegroundColor Gray; & $dotnetPath --version
        Write-Host ""

        $csFiles = (Get-ChildItem -Path $rootDir -Filter "*.cs" -Recurse -Exclude obj,bin).Count
        $xamlFiles = (Get-ChildItem -Path $rootDir -Filter "*.xaml" -Recurse -Exclude obj,bin).Count
        Write-Host "  C# Files:   $csFiles" -ForegroundColor Gray
        Write-Host "  XAML Files: $xamlFiles" -ForegroundColor Gray
        Write-Host ""
    }
    "help" {
        Show-Help
    }
}
