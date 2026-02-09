# ============================================================================
# SOUP Watch Script (Hot Reload)
# ============================================================================
# Usage:
#   .\scripts\watch.ps1                    # Run with hot reload
# ============================================================================

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Watch (Hot Reload)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting with hot reload..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop." -ForegroundColor Gray
Write-Host ""

& $dotnetPath watch run --project $projectFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
