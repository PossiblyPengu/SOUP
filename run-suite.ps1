# Business Tools Suite - Main Launcher
Write-Host "Building and running Business Tools Suite..." -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

$dotnet = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
$project = "BusinessToolsSuite.WPF\src\BusinessToolsSuite.WPF\BusinessToolsSuite.WPF.csproj"

Write-Host "Building project..." -ForegroundColor Yellow
& $dotnet build $project

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful! Starting application..." -ForegroundColor Green
    Write-Host ""
    & $dotnet run --project $project
} else {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
