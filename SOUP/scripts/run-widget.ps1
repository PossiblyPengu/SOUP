# Run only the OrderLog widget window
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"

# Find dotnet (check environment variable, then fallback to system dotnet)
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

Write-Host "Running SOUP widget window..." -ForegroundColor Cyan
if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration Debug --no-build -- --widget
} else {
    & $dotnetPath run --project $projectFile --configuration Debug -- --widget
}
