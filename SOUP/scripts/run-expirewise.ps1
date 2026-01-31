# Run ExpireWise module (uses SOUP project with module flag)
param(
    [switch]$Release,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment (reuse run.ps1 logic)
$localSdkRoot = "D:\CODE\important files"
$localSDKPath = $null
if (Test-Path $localSdkRoot) {
    $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'dotnet-sdk-10*' } |
        Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue

    if (-not $localSDKPath) {
        $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'dotnet-sdk*' } |
            Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue
    }
}

if ($localSDKPath -and (Test-Path $localSDKPath)) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"
$configuration = if ($Release) { "Release" } else { "Debug" }

$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

$moduleName = "expirewise"

# Normalize common aliases to canonical module IDs
$requested = $moduleName.ToLowerInvariant()
$moduleMap = @{
    'expirewise' = 'expirewise'
    'expire' = 'expirewise'
    'allocation' = 'allocation'
    'allocationbuddy' = 'allocation'
    'essentials' = 'essentials'
    'essentialsbuddy' = 'essentials'
    'swiftlabel' = 'swiftlabel'
    'swift' = 'swiftlabel'
    'orderlog' = 'orderlog'
    'order-log' = 'orderlog'
    'funstuff' = 'funstuff'
}
if ($moduleMap.ContainsKey($requested)) {
    $moduleName = $moduleMap[$requested]
} else {
    Write-Warning "Unrecognized module alias '$moduleName' - using as-is"
    $moduleName = $requested
}

Write-Host "Running SOUP (module: $moduleName) - $configuration" -ForegroundColor Cyan

if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration $configuration --no-build -- --module $moduleName --no-widget
} else {
    & $dotnetPath run --project $projectFile --configuration $configuration -- --module $moduleName --no-widget
}
