# ============================================================================
# SOUP Publish Script
# ============================================================================
# Usage:
#   .\scripts\publish.ps1                  # Publish both framework and portable
#   .\scripts\publish.ps1 -Framework       # Publish framework-dependent only
#   .\scripts\publish.ps1 -Portable        # Publish self-contained only
#   .\scripts\publish.ps1 -Installer       # Publish and create installer
#   .\scripts\publish.ps1 -Release         # Publish, tag, and push to GitHub
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
    [switch]$BumpMajor,
    [switch]$BumpMinor,
    [switch]$BumpPatch,
    [string]$SetVersion
)

$ErrorActionPreference = "Stop"

# Setup local .NET SDK environment
$localSDKPath = "D:\CODE\important files\DEPENDANCIES\dotnet-sdk-8.0.404-win-x64"
if (Test-Path $localSDKPath) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$projectFile = Join-Path $srcDir "SOUP.csproj"
$publishFrameworkDir = Join-Path $rootDir "publish-framework"
$publishPortableDir = Join-Path $rootDir "publish-portable"

# Find dotnet (check environment variable, then fallback to system dotnet)
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

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

# Create GitHub release
if ($Release) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Creating GitHub Release" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $tagName = "v$version"
    
    # Check if we're in a git repository
    $gitDir = Join-Path $rootDir ".git"
    if (-not (Test-Path $gitDir)) {
        Write-Host "ERROR: Not a git repository!" -ForegroundColor Red
        exit 1
    }
    
    # Check for uncommitted changes - offer to commit them
    $gitStatus = git -C $rootDir status --porcelain
    if ($gitStatus) {
        Write-Host "You have uncommitted changes:" -ForegroundColor Yellow
        Write-Host $gitStatus -ForegroundColor Gray
        Write-Host ""
        
        $commitChoice = Read-Host "Would you like to commit these changes? (Y/n/abort)"
        if ($commitChoice -eq 'abort' -or $commitChoice -eq 'a') {
            Write-Host "Aborted." -ForegroundColor Yellow
            exit 0
        }
        elseif ($commitChoice -ne 'n' -and $commitChoice -ne 'N') {
            # Stage all changes
            Write-Host "Staging changes..." -ForegroundColor Yellow
            git -C $rootDir add -A
            
            # Get commit message
            $defaultMessage = "Release v$version"
            $commitMessage = Read-Host "Commit message [$defaultMessage]"
            if ([string]::IsNullOrWhiteSpace($commitMessage)) {
                $commitMessage = $defaultMessage
            }
            
            # Commit
            Write-Host "Committing..." -ForegroundColor Yellow
            git -C $rootDir commit -m $commitMessage
            
            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: Failed to commit!" -ForegroundColor Red
                exit 1
            }
            
            # Push
            Write-Host "Pushing to origin..." -ForegroundColor Yellow
            git -C $rootDir push
            
            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: Failed to push! You may need to pull first." -ForegroundColor Red
                exit 1
            }
            
            Write-Host "Changes committed and pushed." -ForegroundColor Green
            Write-Host ""
        }
    }
    
    # Check if tag already exists
    $existingTag = git -C $rootDir tag -l $tagName
    if ($existingTag) {
        Write-Host "WARNING: Tag $tagName already exists!" -ForegroundColor Yellow
        $confirm = Read-Host "Delete and recreate? (y/N)"
        if ($confirm -eq 'y' -or $confirm -eq 'Y') {
            Write-Host "Deleting existing tag..." -ForegroundColor Yellow
            git -C $rootDir tag -d $tagName
            git -C $rootDir push origin --delete $tagName 2>$null
        } else {
            Write-Host "Aborted." -ForegroundColor Yellow
            exit 0
        }
    }
    
    # Run security check before release
    Write-Host "Running security check..." -ForegroundColor Yellow
    $securityScript = Join-Path $rootDir "scripts\security-check.ps1"
    if (Test-Path $securityScript) {
        & $securityScript
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Security check failed! Fix issues before releasing." -ForegroundColor Red
            exit 1
        }
    }
    
    # Create and push tag
    Write-Host "Creating tag $tagName..." -ForegroundColor Yellow
    git -C $rootDir tag -a $tagName -m "Release $tagName"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create tag!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Pushing tag to origin..." -ForegroundColor Yellow
    git -C $rootDir push origin $tagName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to push tag!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "✅ Tag $tagName pushed to GitHub!" -ForegroundColor Green
    Write-Host "GitHub Actions will now build and create the release." -ForegroundColor White
    Write-Host "Check: https://github.com/YOUR_USERNAME/SOUP/actions" -ForegroundColor Cyan
}
