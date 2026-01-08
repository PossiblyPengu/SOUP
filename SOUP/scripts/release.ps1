# ============================================================================
# SOUP Full Release Script
# ============================================================================
# Usage:
#   .\scripts\release.ps1                  # Interactive version bump + full release
#   .\scripts\release.ps1 -Patch           # Bump patch version (4.6.2 -> 4.6.3)
#   .\scripts\release.ps1 -Minor           # Bump minor version (4.6.2 -> 4.7.0)
#   .\scripts\release.ps1 -Major           # Bump major version (4.6.2 -> 5.0.0)
#   .\scripts\release.ps1 -Version 5.0.0   # Set specific version
#   .\scripts\release.ps1 -DryRun          # Preview without making changes
#   .\scripts\release.ps1 -SkipGit         # Build only, don't commit/tag/push
# ============================================================================

param(
    [switch]$Patch,
    [switch]$Minor,
    [switch]$Major,
    [string]$Version,
    [switch]$DryRun,
    [switch]$SkipGit
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
$csprojFile = Join-Path $rootDir "src\SOUP.csproj"
$issFile = Join-Path $rootDir "installer\SOUP.iss"

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  SOUP Full Release Build" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Get current version from csproj
$csprojContent = Get-Content $csprojFile -Raw
if ($csprojContent -match '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    $majorNum = [int]$matches[1]
    $minorNum = [int]$matches[2]
    $patchNum = [int]$matches[3]
    $currentVersion = "$majorNum.$minorNum.$patchNum"
    
    Write-Host "Current version: v$currentVersion" -ForegroundColor Cyan
    
    # Determine new version
    if ($Version) {
        if ($Version -match '^\d+\.\d+\.\d+$') {
            $newVersion = $Version
        } else {
            Write-Host "ERROR: Invalid version format. Use X.Y.Z (e.g., 1.2.3)" -ForegroundColor Red
            exit 1
        }
    }
    elseif ($Major) {
        $newVersion = "$($majorNum + 1).0.0"
    }
    elseif ($Minor) {
        $newVersion = "$majorNum.$($minorNum + 1).0"
    }
    elseif ($Patch) {
        $newVersion = "$majorNum.$minorNum.$($patchNum + 1)"
    }
    else {
        # Interactive mode
        Write-Host ""
        Write-Host "Select version bump type:" -ForegroundColor Yellow
        Write-Host "  [1] Patch  ($majorNum.$minorNum.$($patchNum + 1)) - Bug fixes"
        Write-Host "  [2] Minor  ($majorNum.$($minorNum + 1).0) - New features"
        Write-Host "  [3] Major  ($($majorNum + 1).0.0) - Breaking changes"
        Write-Host "  [4] Custom - Enter specific version"
        Write-Host "  [Q] Quit"
        Write-Host ""
        
        $choice = Read-Host "Choice"
        
        switch ($choice) {
            "1" { $newVersion = "$majorNum.$minorNum.$($patchNum + 1)" }
            "2" { $newVersion = "$majorNum.$($minorNum + 1).0" }
            "3" { $newVersion = "$($majorNum + 1).0.0" }
            "4" {
                $customVersion = Read-Host "Enter version (X.Y.Z)"
                if ($customVersion -match '^\d+\.\d+\.\d+$') {
                    $newVersion = $customVersion
                } else {
                    Write-Host "ERROR: Invalid version format!" -ForegroundColor Red
                    exit 1
                }
            }
            "q" { Write-Host "Aborted." -ForegroundColor Yellow; exit 0 }
            "Q" { Write-Host "Aborted." -ForegroundColor Yellow; exit 0 }
            default {
                Write-Host "ERROR: Invalid choice!" -ForegroundColor Red
                exit 1
            }
        }
    }
    
    Write-Host "New version: v$newVersion" -ForegroundColor Green
    $tagName = "v$newVersion"
    
    # Check if tag already exists
    if (-not $SkipGit) {
        $existingTag = git -C $rootDir tag -l $tagName
        if ($existingTag) {
            Write-Host "ERROR: Tag $tagName already exists!" -ForegroundColor Red
            exit 1
        }
    }
    
    # Dry run check
    if ($DryRun) {
        Write-Host ""
        Write-Host "DRY RUN - No changes will be made" -ForegroundColor Yellow
        Write-Host "  Would update: $currentVersion -> $newVersion" -ForegroundColor Gray
        Write-Host "  Would create tag: $tagName" -ForegroundColor Gray
        exit 0
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
        
        # Update iss (installer)
        $issContent = Get-Content $issFile -Raw
        $issContent = $issContent -replace '#define MyAppVersion "\d+\.\d+\.\d+"', "#define MyAppVersion `"$newVersion`""
        Set-Content $issFile $issContent -NoNewline
        
        # Update AppVersion.cs
        $appVersionFile = Join-Path $srcDir "Core\AppVersion.cs"
        if (Test-Path $appVersionFile) {
            $appVersionContent = Get-Content $appVersionFile -Raw
            $appVersionContent = $appVersionContent -replace 'public const string Version = "[^"]+";', "public const string Version = `"$newVersion`";"
            $today = Get-Date -Format "yyyy-MM-dd"
            $appVersionContent = $appVersionContent -replace 'public const string BuildDate = "[^"]+";', "public const string BuildDate = `"$today`";"
            Set-Content $appVersionFile $appVersionContent -NoNewline
            Write-Host "  Updated AppVersion.cs" -ForegroundColor Green
        }
        
        Write-Host "  Updated to $newVersion" -ForegroundColor Green
        
        # Prompt for changelog entry
        Write-Host ""
        Write-Host "[Changelog] Add release notes for v$newVersion" -ForegroundColor Cyan
        Write-Host "  Enter a title for this release (or press Enter for 'Release Update'):" -ForegroundColor Gray
        $releaseTitle = Read-Host
        if ([string]::IsNullOrWhiteSpace($releaseTitle)) {
            $releaseTitle = "Release Update"
        }
        
        Write-Host "  Enter changelog items (one per line, empty line to finish):" -ForegroundColor Gray
        Write-Host "  Tip: Use emoji prefixes like ✨ 🐛 🔧 🎨 📦 ⚡" -ForegroundColor DarkGray
        $changelogItems = @()
        while ($true) {
            $item = Read-Host "  >"
            if ([string]::IsNullOrWhiteSpace($item)) { break }
            $changelogItems += $item
        }
        
        # Clear any remaining input buffer to prevent echo after script exits
        while ([Console]::KeyAvailable) { $null = [Console]::ReadKey($true) }
        
        if ($changelogItems.Count -gt 0) {
            # Build the changelog entry string
            $itemsString = ($changelogItems | ForEach-Object { "            `"$_`"" }) -join ",`r`n"
            $newEntry = @"
        new("$newVersion", "$today", "$releaseTitle", new[]
        {
$itemsString
        }),

"@
            # Insert after "Changelog { get; } = new List<ChangelogEntry>"
            $appVersionContent = Get-Content $appVersionFile -Raw
            $insertPoint = "Changelog { get; } = new List<ChangelogEntry>`r`n    {`r`n"
            $appVersionContent = $appVersionContent -replace [regex]::Escape($insertPoint), ($insertPoint + $newEntry)
            Set-Content $appVersionFile $appVersionContent -NoNewline
            Write-Host "  Added changelog entry for v$newVersion" -ForegroundColor Green
        } else {
            Write-Host "  No changelog items entered, skipping" -ForegroundColor Yellow
        }
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
Write-Host "  Build Complete!" -ForegroundColor Green
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
Write-Host ""

# Git operations
if (-not $SkipGit) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Git Release" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Run security check
    Write-Host "Running security check..." -ForegroundColor Yellow
    $securityScript = Join-Path $rootDir "scripts\security-check.ps1"
    if (Test-Path $securityScript) {
        & $securityScript
        if (-not $?) {
            Write-Host "ERROR: Security check failed!" -ForegroundColor Red
            exit 1
        }
    }
    
    # Stage and commit
    Write-Host ""
    Write-Host "Committing changes..." -ForegroundColor Yellow
    git -C $rootDir add -A
    
    $commitMessage = "Release v$newVersion"
    git -C $rootDir commit -m $commitMessage
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Nothing to commit or commit failed" -ForegroundColor Yellow
    } else {
        Write-Host "  Committed: $commitMessage" -ForegroundColor Green
    }
    
    # Push commit
    Write-Host ""
    Write-Host "Pushing to origin..." -ForegroundColor Yellow
    git -C $rootDir push
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to push!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Pushed to origin" -ForegroundColor Green
    
    # Create and push tag
    Write-Host ""
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
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Release v$newVersion Published!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "GitHub Actions will now create the release." -ForegroundColor White
    Write-Host "Check: https://github.com/PossiblyPengu/SOUP/actions" -ForegroundColor Cyan
} else {
    Write-Host "Skipped git operations (use without -SkipGit to commit/tag/push)" -ForegroundColor Gray
}
Write-Host ""

# Clear input buffer to prevent any buffered input from echoing after script exits
while ([Console]::KeyAvailable) { $null = [Console]::ReadKey($true) }
