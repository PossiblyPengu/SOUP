# ============================================================================
# MechaRogue Dev Commands
# ============================================================================
# Usage:
#   .\dev.ps1 build    # Quick build (debug)
#   .\dev.ps1 run      # Build and run
#   .\dev.ps1 clean    # Clean build artifacts
#   .\dev.ps1 rebuild  # Clean + build
# ============================================================================

param(
    [Parameter(Position=0)]
    [ValidateSet("build", "run", "clean", "rebuild", "help")]
    [string]$Command = "help"
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment (prefer any installed .NET 9/10 SDK)
$localSdkRoot = "D:\CODE\important files"
$localSDKPath = $null
if (Test-Path $localSdkRoot) {
    $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'dotnet-sdk-*' } |
        Sort-Object Name -Descending |
        Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue
}

if ($localSDKPath -and (Test-Path $localSDKPath)) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
    Write-Host "Using local SDK: $localSDKPath" -ForegroundColor DarkGray
}

# Configuration
$rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $rootDir "MechaRogue.csproj"

function Show-Header($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Show-Help {
    Write-Host ""
    Write-Host "MechaRogue Dev Commands" -ForegroundColor Magenta
    Write-Host "=======================" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  build     " -NoNewline -ForegroundColor Yellow; Write-Host "Quick debug build"
    Write-Host "  run       " -NoNewline -ForegroundColor Yellow; Write-Host "Build and run game"
    Write-Host "  clean     " -NoNewline -ForegroundColor Yellow; Write-Host "Remove build artifacts"
    Write-Host "  rebuild   " -NoNewline -ForegroundColor Yellow; Write-Host "Clean + build"
    Write-Host ""
}

function Invoke-Build {
    Show-Header "Building MechaRogue"
    & dotnet build $projectFile -c Debug
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Invoke-Run {
    Show-Header "Running MechaRogue"
    & dotnet run --project $projectFile -c Debug
}

function Invoke-Clean {
    Show-Header "Cleaning"
    $binDir = Join-Path $rootDir "bin"
    $objDir = Join-Path $rootDir "obj"
    
    if (Test-Path $binDir) { Remove-Item -Recurse -Force $binDir; Write-Host "Removed bin/" }
    if (Test-Path $objDir) { Remove-Item -Recurse -Force $objDir; Write-Host "Removed obj/" }
    Write-Host "Clean complete" -ForegroundColor Green
}

# Execute command
switch ($Command) {
    "build"   { Invoke-Build }
    "run"     { Invoke-Build; Invoke-Run }
    "clean"   { Invoke-Clean }
    "rebuild" { Invoke-Clean; Invoke-Build }
    "help"    { Show-Help }
    default   { Show-Help }
}
