# ============================================================================
# SOUP Quick Commands
# ============================================================================
# Usage:
#   .\scripts\dev.ps1 build                # Quick build (debug)
#   .\scripts\dev.ps1 run                  # Build and run
#   .\scripts\dev.ps1 widget               # Run widget mode
#   .\scripts\dev.ps1 watch                # Hot reload mode
#   .\scripts\dev.ps1 clean                # Clean build artifacts
#   .\scripts\dev.ps1 rebuild              # Clean + build
#   .\scripts\dev.ps1 restore              # Restore NuGet packages
#   .\scripts\dev.ps1 check                # Build with warnings as errors
#   .\scripts\dev.ps1 format               # Format code (if dotnet-format installed)
#   .\scripts\dev.ps1 info                 # Show project info
# ============================================================================

param(
    [Parameter(Position=0)]
    [ValidateSet("build", "run", "widget", "watch", "clean", "rebuild", "restore", "check", "format", "info", "help")]
    [string]$Command = "help"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

function Show-Header($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Show-Help {
    Write-Host ""
    Write-Host "SOUP Dev Commands" -ForegroundColor Cyan
    Write-Host "=================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  build     " -NoNewline -ForegroundColor Yellow; Write-Host "Quick debug build"
    Write-Host "  run       " -NoNewline -ForegroundColor Yellow; Write-Host "Build and run app"
    Write-Host "  widget    " -NoNewline -ForegroundColor Yellow; Write-Host "Run in widget mode"
    Write-Host "  watch     " -NoNewline -ForegroundColor Yellow; Write-Host "Hot reload development"
    Write-Host "  clean     " -NoNewline -ForegroundColor Yellow; Write-Host "Remove build artifacts"
    Write-Host "  rebuild   " -NoNewline -ForegroundColor Yellow; Write-Host "Clean + build"
    Write-Host "  restore   " -NoNewline -ForegroundColor Yellow; Write-Host "Restore NuGet packages"
    Write-Host "  check     " -NoNewline -ForegroundColor Yellow; Write-Host "Build with warnings as errors"
    Write-Host "  format    " -NoNewline -ForegroundColor Yellow; Write-Host "Format code"
    Write-Host "  info      " -NoNewline -ForegroundColor Yellow; Write-Host "Show project info"
    Write-Host ""
}

switch ($Command) {
    "build" {
        Show-Header "Building (Debug)"
        & $dotnetPath build $projectFile --configuration Debug --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "run" {
        Show-Header "Running SOUP"
        & $dotnetPath run --project $projectFile --configuration Debug
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "widget" {
        Show-Header "Running Widget Mode"
        & $dotnetPath run --project $projectFile --configuration Debug -- --widget
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "watch" {
        Show-Header "Hot Reload Mode"
        & $dotnetPath watch run --project $projectFile
    }
    "clean" {
        Show-Header "Cleaning"
        $binDir = Join-Path $srcDir "bin"
        $objDir = Join-Path $srcDir "obj"
        if (Test-Path $binDir) { Remove-Item -Path $binDir -Recurse -Force; Write-Host "  Removed bin/" -ForegroundColor Gray }
        if (Test-Path $objDir) { Remove-Item -Path $objDir -Recurse -Force; Write-Host "  Removed obj/" -ForegroundColor Gray }
        Write-Host "Done!" -ForegroundColor Green
    }
    "rebuild" {
        Show-Header "Rebuilding"
        $binDir = Join-Path $srcDir "bin"
        $objDir = Join-Path $srcDir "obj"
        if (Test-Path $binDir) { Remove-Item -Path $binDir -Recurse -Force }
        if (Test-Path $objDir) { Remove-Item -Path $objDir -Recurse -Force }
        & $dotnetPath build $projectFile --configuration Debug
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "restore" {
        Show-Header "Restoring Packages"
        & $dotnetPath restore $projectFile
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "check" {
        Show-Header "Building with Warnings as Errors"
        & $dotnetPath build $projectFile --configuration Debug -warnaserror
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "format" {
        Show-Header "Formatting Code"
        & $dotnetPath format $projectFile
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    "info" {
        Show-Header "Project Info"
        Write-Host ""
        Write-Host "  Project:  " -NoNewline -ForegroundColor Gray; Write-Host "SOUP"
        Write-Host "  Root:     " -NoNewline -ForegroundColor Gray; Write-Host $rootDir
        Write-Host "  Source:   " -NoNewline -ForegroundColor Gray; Write-Host $srcDir
        Write-Host "  .NET:     " -NoNewline -ForegroundColor Gray; & $dotnetPath --version
        Write-Host ""
        
        # Count files
        $csFiles = (Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse).Count
        $xamlFiles = (Get-ChildItem -Path $srcDir -Filter "*.xaml" -Recurse).Count
        Write-Host "  C# Files:   $csFiles" -ForegroundColor Gray
        Write-Host "  XAML Files: $xamlFiles" -ForegroundColor Gray
        Write-Host ""
    }
    "help" {
        Show-Help
    }
}
