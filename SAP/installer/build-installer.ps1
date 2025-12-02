# SAP Installer Build Script
# This script builds the application and creates an installer

param(
    [string]$Configuration = "Release",
    [string]$DotnetPath = "dotnet",
    [switch]$SkipBuild,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$projectDir = Join-Path $srcDir "SAP"
$projectFile = Join-Path $projectDir "SAP.csproj"
$publishDir = Join-Path $rootDir "publish"
$installerDir = $scriptDir

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  SAP Installer Build Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build and publish the application
if (-not $SkipBuild) {
    Write-Host "[1/3] Building and publishing SAP..." -ForegroundColor Yellow
    
    # Clean previous publish
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }
    
    # Publish as self-contained for Windows x64
    & $DotnetPath publish $projectFile `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDir `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
} else {
    Write-Host "[1/3] Skipping build (using existing publish folder)..." -ForegroundColor Yellow
}

# Step 2: Run the import tool to ensure dictionary database exists
Write-Host ""
Write-Host "[2/3] Ensuring dictionary database is populated..." -ForegroundColor Yellow

$importToolDir = Join-Path $rootDir "tools\ImportDictionary"
Push-Location $importToolDir
try {
    & $DotnetPath run
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Import tool failed. Dictionary may need manual import." -ForegroundColor Yellow
    } else {
        Write-Host "Dictionary database ready!" -ForegroundColor Green
    }
} finally {
    Pop-Location
}

# Step 3: Create the installer
if (-not $SkipInstaller) {
    Write-Host ""
    Write-Host "[3/3] Creating installer..." -ForegroundColor Yellow
    
    # Check if Inno Setup is installed
    $innoSetupPath = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if (-not $innoSetupPath) {
        Write-Host ""
        Write-Host "Inno Setup 6 not found!" -ForegroundColor Red
        Write-Host "Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "The published application is available at:" -ForegroundColor Cyan
        Write-Host "  $publishDir" -ForegroundColor White
        exit 1
    }
    
    $issFile = Join-Path $installerDir "SAP.iss"
    
    Write-Host "Using Inno Setup: $innoSetupPath"
    & $innoSetupPath $issFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Installer creation failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Installer created successfully!" -ForegroundColor Green
} else {
    Write-Host "[3/3] Skipping installer creation..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Published files: $publishDir" -ForegroundColor White

$outputDir = Join-Path $installerDir "Output"
if (Test-Path $outputDir) {
    $installerFile = Get-ChildItem -Path $outputDir -Filter "*.exe" | Select-Object -First 1
    if ($installerFile) {
        Write-Host "Installer: $($installerFile.FullName)" -ForegroundColor White
    }
}
