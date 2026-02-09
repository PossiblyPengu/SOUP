# ============================================================================
# SOUP Run Module (unified launcher)
# ============================================================================
# Usage (called by individual run-*.ps1 wrappers):
#   .\scripts\run-module.ps1 -Module allocation -DisplayName "AllocationBuddy" -Color Blue
#   .\scripts\run-module.ps1 -Module widget    -DisplayName "OrderLog Widget (Standalone)" -Color Green -ExtraArgs "--widget"
# ============================================================================

param(
    [Parameter(Mandatory)]
    [string]$Module,

    [Parameter(Mandatory)]
    [string]$DisplayName,

    [string]$Color = "Cyan",

    [string[]]$ExtraArgs,

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

$configuration = "Debug"

Write-Host ""
Write-Host "========================================" -ForegroundColor $Color
Write-Host "  SOUP - $DisplayName" -ForegroundColor $Color
Write-Host "========================================" -ForegroundColor $Color
Write-Host ""

# Build the runtime arguments passed after "--"
$runtimeArgs = if ($ExtraArgs) { $ExtraArgs } else { @("--module", $Module, "--no-widget") }

if ($NoBuild) {
    $exePath = Join-Path $srcDir "bin\$configuration\net10.0-windows10.0.19041.0\win-x64\SOUP.exe"

    if (-not (Test-Path $exePath)) {
        Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
        Write-Host "Run without -NoBuild to build first." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Running $DisplayName (no build)..." -ForegroundColor Yellow
    & $exePath @runtimeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "Building and running $DisplayName..." -ForegroundColor Yellow
    & $dotnetPath run --project $projectFile --configuration $configuration -- @runtimeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
