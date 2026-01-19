# Run only the OrderLog widget window
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment
$localSDKPath = "D:\CODE\important files\dotnet-sdk-10.0.101-win-x64"
if (Test-Path $localSDKPath) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

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
