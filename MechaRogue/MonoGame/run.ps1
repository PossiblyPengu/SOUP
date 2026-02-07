#!/usr/bin/env pwsh
# Run MechaRogue MonoGame build
param(
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
$config = if ($Release) { 'Release' } else { 'Debug' }
$projectDir = Split-Path $MyInvocation.MyCommand.Path -Parent

# Ensure .NET 10 SDK is on PATH
$sdkPath = "D:\CODE\important files\dotnet-sdk-10.0.101-win-x64"
if (Test-Path $sdkPath) {
    $env:PATH = "$sdkPath;$env:PATH"
}

Push-Location "$projectDir"
try {
    Write-Host "Building MechaRogue (MonoGame) [$config]..." -ForegroundColor Cyan
    dotnet run -c $config
}
finally {
    Pop-Location
}
