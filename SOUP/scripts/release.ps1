# ============================================================================
# SOUP Full Release Script
# ============================================================================
# Usage:
#   .\scripts\release.ps1                  # Full release build + installer
#   .\scripts\release.ps1 -BumpMinor       # Bump minor version first
#   .\scripts\release.ps1 -BumpPatch       # Bump patch version first
# ============================================================================

param(
    [switch]$BumpMinor,
    [switch]$BumpPatch
)

$ErrorActionPreference = "Stop"

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csprojFile = Join-Path $rootDir "src\SOUP\SOUP.csproj"
$issFile = Join-Path $rootDir "installer\SOUP.iss"

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  SOUP Full Release Build" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Get current version from csproj
$csprojContent = Get-Content $csprojFile -Raw
if ($csprojContent -match '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]$matches[3]
    $currentVersion = "$major.$minor.$patch"
    
    Write-Host "Current version: $currentVersion" -ForegroundColor Cyan
    
    # Bump version if requested
    if ($BumpMinor) {
        $minor++
        $patch = 0
        $newVersion = "$major.$minor.$patch"
        Write-Host "Bumping to: $newVersion" -ForegroundColor Yellow
    } elseif ($BumpPatch) {
        $patch++
        $newVersion = "$major.$minor.$patch"
        Write-Host "Bumping to: $newVersion" -ForegroundColor Yellow
    } else {
        $newVersion = $currentVersion
    }
    
    # Update version in files if changed
    if ($newVersion -ne $currentVersion) {
        Write-Host ""
        Write-Host "[Version] Updating version numbers..." -ForegroundColor Yellow
        
        # Update csproj
        $csprojContent = $csprojContent -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>"
        $csprojContent = $csprojContent -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newVersion.0</AssemblyVersion>"
        $csprojContent = $csprojContent -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newVersion.0</FileVersion>"
        Set-Content $csprojFile $csprojContent -NoNewline
        
        # Update iss
        $issContent = Get-Content $issFile -Raw
        $issContent = $issContent -replace '#define MyAppVersion "\d+\.\d+\.\d+"', "#define MyAppVersion `"$newVersion`""
        Set-Content $issFile $issContent -NoNewline
        
        Write-Host "  Updated to $newVersion" -ForegroundColor Green
    }
} else {
    Write-Host "WARNING: Could not parse version from csproj" -ForegroundColor Yellow
    $newVersion = "unknown"
}

Write-Host ""

# Step 1: Clean
Write-Host "[1/4] Cleaning..." -ForegroundColor Yellow
& "$rootDir\scripts\clean.ps1" -All
Write-Host ""

# Step 2: Build Release
Write-Host "[2/4] Building Release..." -ForegroundColor Yellow
& "$rootDir\scripts\build.ps1" -Release -Clean -Restore
Write-Host ""

# Step 3: Publish
Write-Host "[3/4] Publishing..." -ForegroundColor Yellow
& "$rootDir\scripts\publish.ps1"
Write-Host ""

# Step 4: Create Installer
Write-Host "[4/4] Creating installer..." -ForegroundColor Yellow
& "$rootDir\scripts\publish.ps1" -Installer
Write-Host ""

Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  Release $newVersion Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Show output files
$installerFile = Get-ChildItem -Path (Join-Path $rootDir "installer") -Filter "SOUP-Setup-*.exe" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

Write-Host "Release artifacts:" -ForegroundColor White
Write-Host "  Installer: $($installerFile.FullName)" -ForegroundColor Cyan
Write-Host "  Framework: $(Join-Path $rootDir 'publish-framework')" -ForegroundColor White
Write-Host "  Portable:  $(Join-Path $rootDir 'publish-portable')" -ForegroundColor White
