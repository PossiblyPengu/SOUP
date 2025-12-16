# ============================================================================
# SOUP Publish Script
# ============================================================================
# Usage:
#   .\scripts\publish.ps1                  # Publish both framework and portable
#   .\scripts\publish.ps1 -Framework       # Publish framework-dependent only
#   .\scripts\publish.ps1 -Portable        # Publish self-contained only
#   .\scripts\publish.ps1 -Installer       # Publish and create installer
# ============================================================================

param(
    [switch]$Framework,
    [switch]$Portable,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP\SOUP.csproj"
$publishFrameworkDir = Join-Path $rootDir "publish-framework"
$publishPortableDir = Join-Path $rootDir "publish-portable"

# Find dotnet
$dotnetPath = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
if (-not (Test-Path $dotnetPath)) {
    $dotnetPath = "dotnet"
}

# Default to both if neither specified
if (-not $Framework -and -not $Portable -and -not $Installer) {
    $Framework = $true
    $Portable = $true
}

# If installer is requested, we need both
if ($Installer) {
    $Framework = $true
    $Portable = $true
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Publish" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Publish framework-dependent
if ($Framework) {
    Write-Host "[1/3] Publishing framework-dependent version..." -ForegroundColor Yellow
    
    if (Test-Path $publishFrameworkDir) {
        Remove-Item -Path $publishFrameworkDir -Recurse -Force
    }
    
    & $dotnetPath publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        --output $publishFrameworkDir `
        -p:PublishSingleFile=false
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Framework-dependent publish failed!" -ForegroundColor Red
        exit 1
    }
    
    $size = (Get-ChildItem $publishFrameworkDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "  Published! Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
}

# Publish self-contained
if ($Portable) {
    Write-Host "[2/3] Publishing self-contained (portable) version..." -ForegroundColor Yellow
    
    if (Test-Path $publishPortableDir) {
        Remove-Item -Path $publishPortableDir -Recurse -Force
    }
    
    & $dotnetPath publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishPortableDir `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Self-contained publish failed!" -ForegroundColor Red
        exit 1
    }
    
    $size = (Get-ChildItem $publishPortableDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "  Published! Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
}

# Create installer
if ($Installer) {
    Write-Host "[3/3] Creating installer..." -ForegroundColor Yellow
    
    $innoSetupPath = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if (-not $innoSetupPath) {
        Write-Host "ERROR: Inno Setup 6 not found!" -ForegroundColor Red
        Write-Host "Install from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        exit 1
    }
    
    $issFile = Join-Path $rootDir "installer\SOUP.iss"
    & $innoSetupPath $issFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Installer creation failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "  Installer created!" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Publish Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Output locations:" -ForegroundColor White
if ($Framework) {
    Write-Host "  Framework: $publishFrameworkDir" -ForegroundColor White
}
if ($Portable) {
    Write-Host "  Portable:  $publishPortableDir" -ForegroundColor White
}
if ($Installer) {
    $installerFile = Get-ChildItem -Path (Join-Path $rootDir "installer") -Filter "SOUP-Setup-*.exe" | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 1
    if ($installerFile) {
        Write-Host "  Installer: $($installerFile.FullName)" -ForegroundColor White
    }
}
