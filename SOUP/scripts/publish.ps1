# ============================================================================
# SOUP Publish Script
# ============================================================================
# Usage:
#   .\scripts\publish.ps1                  # Publish both framework and portable
#   .\scripts\publish.ps1 -Framework       # Publish framework-dependent only
#   .\scripts\publish.ps1 -Portable        # Publish self-contained only
#   .\scripts\publish.ps1 -Installer       # Publish and create installer
#   .\scripts\publish.ps1 -Release         # (Deprecated: use release.ps1 instead)
#   .\scripts\publish.ps1 -BumpMajor       # Bump major version (1.0.0 -> 2.0.0)
#   .\scripts\publish.ps1 -BumpMinor       # Bump minor version (1.0.0 -> 1.1.0)
#   .\scripts\publish.ps1 -BumpPatch       # Bump patch version (1.0.0 -> 1.0.1)
#   .\scripts\publish.ps1 -SetVersion 1.2.3  # Set specific version
# ============================================================================

param(
    [switch]$Framework,
    [switch]$Portable,
    [switch]$Installer,
    [switch]$Release,
    [switch]$Trim,
    [switch]$InvariantGlobalization,
    [switch]$BumpMajor,
    [switch]$BumpMinor,
    [switch]$BumpPatch,
    [string]$SetVersion
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

$publishFrameworkDir = Join-Path $rootDir "publish-framework"
$publishPortableDir = Join-Path $rootDir "publish-portable"

# Default to both if neither specified
if (-not $Framework -and -not $Portable -and -not $Installer -and -not $Release) {
    $Framework = $true
    $Portable = $true
}

# If installer is requested, we need both
if ($Installer) {
    $Framework = $true
    $Portable = $true
}

# If release is requested, we need both published
if ($Release) {
    $Framework = $true
    $Portable = $true
}

# Get version from csproj
$csprojContent = Get-Content $projectFile -Raw
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $version = $matches[1]
} else {
    Write-Host "ERROR: Could not find version in csproj!" -ForegroundColor Red
    exit 1
}

# Detect WPF usage: trimming is not supported for WPF apps
$isWpf = $false
if ($csprojContent -match '<UseWPF>\s*true\s*</UseWPF>') { $isWpf = $true }

# Parse current version
$versionParts = $version -split '\.'
$major = [int]$versionParts[0]
$minor = [int]$versionParts[1]
$patch = [int]$versionParts[2]

# Handle version changes
$versionChanged = $false

if ($SetVersion) {
    if ($SetVersion -match '^\d+\.\d+\.\d+$') {
        $version = $SetVersion
        $versionChanged = $true
    } else {
        Write-Host "ERROR: Invalid version format. Use X.Y.Z (e.g., 1.2.3)" -ForegroundColor Red
        exit 1
    }
}
elseif ($BumpMajor) {
    $major++
    $minor = 0
    $patch = 0
    $version = "$major.$minor.$patch"
    $versionChanged = $true
}
elseif ($BumpMinor) {
    $minor++
    $patch = 0
    $version = "$major.$minor.$patch"
    $versionChanged = $true
}
elseif ($BumpPatch) {
    $patch++
    $version = "$major.$minor.$patch"
    $versionChanged = $true
}

# Update version in csproj if changed
if ($versionChanged) {
    Write-Host "Updating version to $version..." -ForegroundColor Yellow
    
    # Update Version
    $csprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$version</Version>"
    
    # Update AssemblyVersion
    $csprojContent = $csprojContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$version.0</AssemblyVersion>"
    
    # Update FileVersion
    $csprojContent = $csprojContent -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$version.0</FileVersion>"
    
    # Write back csproj
    Set-Content -Path $projectFile -Value $csprojContent -NoNewline
    
    # Update AppVersion.cs
    $appVersionFile = Join-Path $srcDir "Core\AppVersion.cs"
    if (Test-Path $appVersionFile) {
        $appVersionContent = Get-Content $appVersionFile -Raw
        $appVersionContent = $appVersionContent -replace 'public const string Version = "[^"]+";', "public const string Version = `"$version`";"
        $today = Get-Date -Format "yyyy-MM-dd"
        $appVersionContent = $appVersionContent -replace 'public const string BuildDate = "[^"]+";', "public const string BuildDate = `"$today`";"
        Set-Content $appVersionFile $appVersionContent -NoNewline
    }
    
    Write-Host "Version updated to $version" -ForegroundColor Green
    Write-Host ""
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Publish v$version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Publish framework-dependent
if ($Framework) {
    Write-Host "[1/3] Publishing framework-dependent version..." -ForegroundColor Yellow
    
    if (Test-Path $publishFrameworkDir) {
        Remove-Item -Path $publishFrameworkDir -Recurse -Force
    }
    
    $frameworkArgs = @('-p:PublishSingleFile=false')
    if ($InvariantGlobalization) { $frameworkArgs += '-p:InvariantGlobalization=true' }

    & $dotnetPath publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        --output $publishFrameworkDir `
        $frameworkArgs
    
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
    
    $portableArgs = @(
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true'
    )
    if ($Trim) {
        if ($isWpf) {
            Write-Host "  Skipping PublishTrimmed for WPF project (not supported)." -ForegroundColor Yellow
        } else {
            $portableArgs += '-p:PublishTrimmed=true'
        }
    }
    if ($InvariantGlobalization) { $portableArgs += '-p:InvariantGlobalization=true' }

    & $dotnetPath publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishPortableDir `
        $portableArgs
    
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

# Generate version.json and portable zip for local update server
$publishDir = Join-Path $rootDir "publish"
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}

# Create portable zip for auto-update
$portableZipPath = Join-Path $publishDir "SOUP-portable.zip"
if (Test-Path $publishPortableDir) {
    Write-Host "Creating portable zip for auto-update..." -ForegroundColor Yellow
    
    # Remove old zip if exists
    if (Test-Path $portableZipPath) {
        Remove-Item $portableZipPath -Force
    }
    
    # Create the zip
    Compress-Archive -Path "$publishPortableDir\*" -DestinationPath $portableZipPath -Force
    Write-Host "  Created SOUP-portable.zip" -ForegroundColor Green
}

$portableSize = 0
if (Test-Path $portableZipPath) {
    $portableSize = (Get-Item $portableZipPath).Length
}

$today = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
$versionJson = @{
    version = $version
    releaseNotes = "SOUP v$version"
    downloadUrl = "http://localhost:8080/SOUP-portable.zip"
    publishedAt = $today
    assetName = "SOUP-portable.zip"
    assetSize = $portableSize
} | ConvertTo-Json -Depth 2

$versionJsonPath = Join-Path $publishDir "version.json"
Set-Content -Path $versionJsonPath -Value $versionJson
Write-Host "  Generated version.json for update server" -ForegroundColor Green

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
Write-Host "  version.json: $versionJsonPath" -ForegroundColor White
if ($Installer) {
    $installerFile = Get-ChildItem -Path (Join-Path $rootDir "installer") -Filter "SOUP-Setup-*.exe" | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 1
    if ($installerFile) {
        Write-Host "  Installer: $($installerFile.FullName)" -ForegroundColor White
    }
}

# Create GitHub release (delegated to release.ps1)
if ($Release) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  Use release.ps1 for full releases" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "The -Release flag is deprecated here." -ForegroundColor Yellow
    Write-Host "Run instead:  .\scripts\release.ps1 -Patch" -ForegroundColor Cyan
    Write-Host "  release.ps1 handles:" -ForegroundColor Gray
    Write-Host "    - Version bump + changelog" -ForegroundColor Gray
    Write-Host "    - Clean/build/publish" -ForegroundColor Gray
    Write-Host "    - Scoped git commit + tag + push" -ForegroundColor Gray
}
