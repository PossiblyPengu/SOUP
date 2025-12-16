# SAP Installer Build Script
# This script builds the application and creates an installer
# Produces two versions:
#   - Framework-dependent (smaller, requires .NET 8 runtime) for standard install
#   - Self-contained (larger, no dependencies) for portable install

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
$publishFrameworkDir = Join-Path $rootDir "publish-framework"
$publishPortableDir = Join-Path $rootDir "publish-portable"
$installerDir = $scriptDir

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  SAP Installer Build Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build and publish the application (two versions)
if (-not $SkipBuild) {
    Write-Host "[1/4] Building framework-dependent version (standard install)..." -ForegroundColor Yellow
    
    # Clean previous publishes
    if (Test-Path $publishFrameworkDir) {
        Remove-Item -Path $publishFrameworkDir -Recurse -Force
    }
    if (Test-Path $publishPortableDir) {
        Remove-Item -Path $publishPortableDir -Recurse -Force
    }
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }
    
    # Publish framework-dependent version (smaller, requires .NET runtime)
    # Note: Framework-dependent doesn't support single-file, so we publish as folder
    & $DotnetPath publish $projectFile `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained false `
        --output $publishFrameworkDir `
        -p:PublishSingleFile=false `
        -p:EnableCompressionInSingleFile=false
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Framework-dependent build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Framework-dependent build completed!" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "[2/4] Building self-contained version (portable install)..." -ForegroundColor Yellow
    
    # Publish self-contained version (larger, no dependencies) as single file
    & $DotnetPath publish $projectFile `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output $publishPortableDir `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Self-contained build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Self-contained build completed!" -ForegroundColor Green
    
    # Show sizes
    $frameworkSize = (Get-ChildItem $publishFrameworkDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    $portableSize = (Get-ChildItem $publishPortableDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host ""
    Write-Host "Framework-dependent size: $([math]::Round($frameworkSize, 2)) MB" -ForegroundColor Cyan
    Write-Host "Self-contained size: $([math]::Round($portableSize, 2)) MB" -ForegroundColor Cyan
    
} else {
    Write-Host "[1/4] Skipping build (using existing publish folders)..." -ForegroundColor Yellow
    Write-Host "[2/4] Skipping build (using existing publish folders)..." -ForegroundColor Yellow
}

# Step 2: Run the import tool to ensure dictionary database exists
Write-Host ""
Write-Host "[3/4] Ensuring dictionary database is populated..." -ForegroundColor Yellow

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
    Write-Host "[4/4] Creating installer..." -ForegroundColor Yellow
    
    # Check if Inno Setup is installed
    $innoSetupPath = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if (-not $innoSetupPath) {
        Write-Host ""
        Write-Host "Inno Setup 6 not found!" -ForegroundColor Red
        Write-Host "Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "The published applications are available at:" -ForegroundColor Cyan
        Write-Host "  Framework-dependent: $publishFrameworkDir" -ForegroundColor White
        Write-Host "  Self-contained:      $publishPortableDir" -ForegroundColor White
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
Write-Host "Published files:" -ForegroundColor White
Write-Host "  Framework-dependent: $publishFrameworkDir" -ForegroundColor White
Write-Host "  Self-contained:      $publishPortableDir" -ForegroundColor White

$outputDir = Join-Path $installerDir "Output"
if (Test-Path $outputDir) {
    $installerFile = Get-ChildItem -Path $outputDir -Filter "*.exe" | Select-Object -First 1
    if ($installerFile) {
        Write-Host "Installer: $($installerFile.FullName)" -ForegroundColor White
    }
}
