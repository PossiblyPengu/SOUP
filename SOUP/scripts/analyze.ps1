# ============================================================================
# SOUP Analyze Script - Code Quality Checks
# ============================================================================
# Usage:
#   .\scripts\analyze.ps1                  # Run all analyzers
#   .\scripts\analyze.ps1 -Quick           # Quick analysis (no build)
#   .\scripts\analyze.ps1 -Fix             # Auto-fix what's possible
# ============================================================================

param(
    [switch]$Quick,
    [switch]$Fix
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Code Analysis" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$issues = 0

# 1. Check for build warnings
Write-Host "[1/4] Checking build warnings..." -ForegroundColor Yellow
if ($Quick) {
    Write-Host "  Skipped (quick mode)" -ForegroundColor Gray
    $warnings = @()
} else {
    $buildOutput = & $dotnetPath build $projectFile --configuration Debug --no-restore 2>&1
    $warnings = $buildOutput | Select-String -Pattern "warning [A-Z]+[0-9]+"
}
if ($warnings) {
    Write-Host "  Found $($warnings.Count) warning(s):" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    $issues += $warnings.Count
} else {
    Write-Host "  No warnings!" -ForegroundColor Green
}

# 2. Check for TODO/FIXME comments
Write-Host ""
Write-Host "[2/4] Checking for TODO/FIXME comments..." -ForegroundColor Yellow
$todoPattern = "TODO|FIXME|HACK|XXX"
$todos = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | 
    Select-String -Pattern $todoPattern -CaseSensitive:$false
if ($todos) {
    Write-Host "  Found $($todos.Count) TODO/FIXME comment(s):" -ForegroundColor Yellow
    $todos | Group-Object Filename | ForEach-Object {
        Write-Host "    $($_.Name): $($_.Count)" -ForegroundColor Gray
    }
} else {
    Write-Host "  No TODO/FIXME comments!" -ForegroundColor Green
}

# 3. Check for large files
Write-Host ""
Write-Host "[3/4] Checking for large files (>500 lines)..." -ForegroundColor Yellow
$largeFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | 
    ForEach-Object {
        $lines = (Get-Content $_.FullName).Count
        if ($lines -gt 500) {
            [PSCustomObject]@{ Name = $_.Name; Lines = $lines; Path = $_.FullName.Replace($rootDir, "") }
        }
    } | Sort-Object Lines -Descending
if ($largeFiles) {
    Write-Host "  Found $($largeFiles.Count) large file(s):" -ForegroundColor Yellow
    $largeFiles | ForEach-Object { Write-Host "    $($_.Lines) lines: $($_.Path)" -ForegroundColor Gray }
} else {
    Write-Host "  No large files!" -ForegroundColor Green
}

# 4. Check for empty catch blocks
Write-Host ""
Write-Host "[4/4] Checking for empty catch blocks..." -ForegroundColor Yellow
$emptyCatch = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | 
    Select-String -Pattern "catch\s*\([^)]*\)\s*\{\s*\}" 
if ($emptyCatch) {
    Write-Host "  Found $($emptyCatch.Count) empty catch block(s):" -ForegroundColor Yellow
    $emptyCatch | ForEach-Object { Write-Host "    $($_.Filename):$($_.LineNumber)" -ForegroundColor Gray }
    $issues += $emptyCatch.Count
} else {
    Write-Host "  No empty catch blocks!" -ForegroundColor Green
}

# Auto-fix: format code if -Fix is specified
if ($Fix) {
    Write-Host ""
    Write-Host "[Fix] Running dotnet format..." -ForegroundColor Yellow
    & $dotnetPath format $projectFile
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Code formatted!" -ForegroundColor Green
    } else {
        Write-Host "  Format failed or not available." -ForegroundColor Yellow
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($issues -eq 0) {
    Write-Host "  All checks passed!" -ForegroundColor Green
} else {
    Write-Host "  Found $issues issue(s) to review" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
