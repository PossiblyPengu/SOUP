# ============================================================================
# SOUP Test Script
# ============================================================================
# Usage:
#   .\scripts\test.ps1                     # Run all tests
#   .\scripts\test.ps1 -Filter "OrderLog"  # Run tests matching filter
#   .\scripts\test.ps1 -Coverage           # Run with code coverage
#   .\scripts\test.ps1 -DetailedOutput     # Verbose output
# ============================================================================

param(
    [string]$Filter,
    [switch]$Coverage,
    [switch]$DetailedOutput
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Test Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if test project exists
$testProject = Join-Path $rootDir "tests\SOUP.Tests\SOUP.Tests.csproj"
if (-not (Test-Path $testProject)) {
    Write-Host "No test project found at: $testProject" -ForegroundColor Yellow
    Write-Host "To create tests, add a test project in tests/SOUP.Tests/" -ForegroundColor Gray
    exit 0
}

# Build test arguments
$testArgs = @("test", $testProject)

if ($Filter) {
    $testArgs += "--filter"
    $testArgs += $Filter
    Write-Host "Filter: $Filter" -ForegroundColor Yellow
}

if ($DetailedOutput) {
    $testArgs += "--verbosity"
    $testArgs += "detailed"
}

if ($Coverage) {
    $testArgs += "--collect:""XPlat Code Coverage"""
    Write-Host "Coverage: Enabled" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Running tests..." -ForegroundColor Yellow
& $dotnetPath @testArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Some tests failed!" -ForegroundColor Red
    exit 1
}
