# Run only the OrderLog widget window
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment (prefer .NET 10 if present)
$sdkCandidates = @(
    "D:\CODE\important files\dotnet-sdk-10.0.101-win-x64",
    "D:\CODE\important files\dotnet-sdk-9.0.306-win-x64"
)
$localSDKPath = $sdkCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($localSDKPath) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"

# Find dotnet (prefer dotnet.exe in DOTNET_ROOT, then DOTNET_PATH, then system)
if ($env:DOTNET_ROOT -and (Test-Path (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) {
    $dotnetPath = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
} elseif ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) {
    $dotnetPath = $env:DOTNET_PATH
} else {
    $dotnetPath = "dotnet"
}

Write-Host "Running SOUP widget window..." -ForegroundColor Cyan
if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration Debug --no-build -- --widget
} else {
    & $dotnetPath run --project $projectFile --configuration Debug -- --widget
}
