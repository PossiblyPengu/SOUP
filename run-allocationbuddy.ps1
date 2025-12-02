# AllocationBuddy - Standalone Application
Write-Host "Building and running AllocationBuddy..." -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

$dotnet = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
$project = "SAP\src\AllocationBuddy.Standalone\AllocationBuddy.Standalone.csproj"
$exe = "SAP\src\AllocationBuddy.Standalone\bin\Debug\net8.0-windows\AllocationBuddy.exe"

Write-Host "Building project..." -ForegroundColor Yellow
& "$dotnet" build $project

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful! Starting AllocationBuddy..." -ForegroundColor Green
    Write-Host ""
    & "$exe"
} else {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
