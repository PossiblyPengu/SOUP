# Run only the OrderLog widget window
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"

# dotnet path (use local SDK path if present)
$dotnetPath = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
if (-not (Test-Path $dotnetPath)) { $dotnetPath = "dotnet" }

Write-Host "Running SOUP widget window..." -ForegroundColor Cyan
if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration Debug --no-build -- --widget
} else {
    & $dotnetPath run --project $projectFile --configuration Debug -- --widget
}
