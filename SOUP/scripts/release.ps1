# ============================================================================
# SOUP Release Script (GitHub + Local Publishing)
# ============================================================================
# Usage:
#   .\scripts\release.ps1                  # Interactive version bump + release
#   .\scripts\release.ps1 -Patch           # Bump patch version (4.6.2 -> 4.6.3)
#   .\scripts\release.ps1 -Minor           # Bump minor version (4.6.2 -> 4.7.0)
#   .\scripts\release.ps1 -Major           # Bump major version (4.6.2 -> 5.0.0)
#   .\scripts\release.ps1 -Bump patch      # Equivalent to -Patch
#   .\scripts\release.ps1 -Bump minor      # Equivalent to -Minor
#   .\scripts\release.ps1 -Bump major      # Equivalent to -Major
#   .\scripts\release.ps1 -Version 5.0.0   # Set specific version (also supports prerelease like 5.0.0-beta.1)
#   .\scripts\release.ps1 -DryRun          # Preview without making changes
#   .\scripts\release.ps1 -SkipGit         # Build only, don't commit/tag/push
#   .\scripts\release.ps1 -Notes "note"    # Single-line release note
# ============================================================================

param(
    [switch]$Patch,
    [switch]$Minor,
    [switch]$Major,
    [string]$Version,
    [string]$Bump,
    [switch]$DryRun,
    [switch]$SkipGit,
    [string]$Notes
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  SOUP Release" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Get current version from csproj
$csprojContent = Get-Content $projectFile -Raw
# Match version with optional prerelease suffix (e.g., 5.0.1, 5.0.1-beta, 5.0.1-beta.1)
if ($csprojContent -match '<Version>(\d+)\.(\d+)\.(\d+)(-[A-Za-z0-9\.-]+)?</Version>') {
    $majorNum = [int]$matches[1]
    $minorNum = [int]$matches[2]
    $patchNum = [int]$matches[3]
    $prereleaseTag = $matches[4]  # May be $null or like "-beta"
    $currentVersion = "$majorNum.$minorNum.$patchNum$prereleaseTag"
    
    Write-Host "Current version: v$currentVersion" -ForegroundColor Cyan
    
    # Determine new version
    if ($Version) {
        # Accept semver with optional prerelease (e.g. 1.2.3 or 1.2.3-beta.1)
        if ($Version -match '^\d+\.\d+\.\d+(-[A-Za-z0-9\.-]+)?$') {
            $newVersion = $Version
        } else {
            Write-Host "ERROR: Invalid version format. Use X.Y.Z or X.Y.Z-prerelease" -ForegroundColor Red
            exit 1
        }
    }
    elseif ($Bump) {
        switch ($Bump.ToLower()) {
            'major' { $newVersion = "$($majorNum + 1).0.0" }
            'minor' { $newVersion = "$majorNum.$($minorNum + 1).0" }
            'patch' { $newVersion = "$majorNum.$minorNum.$($patchNum + 1)" }
            default {
                # If Bump looks like a version, accept it (allow prerelease)
                if ($Bump -match '^\d+\.\d+\.\d+(-[A-Za-z0-9\.-]+)?$') {
                    $newVersion = $Bump
                } else {
                    Write-Host "ERROR: Invalid bump value. Use 'major','minor','patch', or a version string like X.Y.Z" -ForegroundColor Red
                    exit 1
                }
            }
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
        Write-Host "Select version bump:" -ForegroundColor Yellow
        Write-Host "  [1] Patch  ($majorNum.$minorNum.$($patchNum + 1))"
        Write-Host "  [2] Minor  ($majorNum.$($minorNum + 1).0)"
        Write-Host "  [3] Major  ($($majorNum + 1).0.0)"
        Write-Host "  [4] Custom (enter full version like 5.0.0 or 5.0.0-beta.1)"
        Write-Host "  [Q] Quit"
        Write-Host ""
        
        $choice = Read-Host "Choice"
        
        switch ($choice) {
            "1" { $newVersion = "$majorNum.$minorNum.$($patchNum + 1)" }
            "2" { $newVersion = "$majorNum.$($minorNum + 1).0" }
            "3" { $newVersion = "$($majorNum + 1).0.0" }
            "4" {
                $inputVer = Read-Host "Enter version (X.Y.Z or X.Y.Z-prerelease)"
                if ($inputVer -match '^\d+\.\d+\.\d+(-[A-Za-z0-9\.-]+)?$') { $newVersion = $inputVer } else { Write-Host "Invalid version format" -ForegroundColor Red; exit 1 }
            }
            "q" { exit 0 }
            default { Write-Host "Invalid choice" -ForegroundColor Red; exit 1 }
        }
    }
    
    Write-Host "New version: v$newVersion" -ForegroundColor Green
    $tagName = "v$newVersion"
    
    # Get release notes
    $releaseNotes = @()
    if ($Notes) {
        $releaseNotes = @($Notes)
    } else {
        Write-Host ""
        Write-Host "[Patch Notes] Enter changes (empty line to finish):" -ForegroundColor Yellow
        Write-Host "  Tip: Start with emoji like * bug fix" -ForegroundColor DarkGray
        while ($true) {
            $line = Read-Host "  >"
            if ([string]::IsNullOrWhiteSpace($line)) { break }
            $releaseNotes += $line
        }
    }
    
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
        Write-Host "  Version:  $currentVersion -> $newVersion" -ForegroundColor Gray
        Write-Host "  Tag:      $tagName" -ForegroundColor Gray
        Write-Host "  Files:    src\SOUP.csproj, src\Core\AppVersion.cs" -ForegroundColor Gray
        if ($releaseNotes.Count -gt 0) {
            Write-Host "  Notes:" -ForegroundColor Gray
            $releaseNotes | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
        }
        if (-not $SkipGit) {
            Write-Host "  Git:      commit + tag + push to origin" -ForegroundColor Gray
        } else {
            Write-Host "  Git:      skipped" -ForegroundColor Gray
        }
        exit 0
    }
    
    # Prepare backups so we can roll back if build/publish fail
    $originalCsprojContent = $csprojContent
    $appVersionFile = Join-Path $srcDir "Core\AppVersion.cs"
    $appVersionFileExists = Test-Path $appVersionFile
    # Track which files we modify for scoped git add
    $modifiedFiles = @($projectFile)
    if ($appVersionFileExists) {
        $originalAppVersionContent = Get-Content $appVersionFile -Raw
    } else {
        $originalAppVersionContent = $null
    }

    function Restore-OriginalFiles {
        if ($null -ne $originalCsprojContent) {
            Write-Host "Restoring original csproj content..." -ForegroundColor Yellow
            Set-Content $projectFile $originalCsprojContent -NoNewline
        }
        if ($appVersionFileExists -and $null -ne $originalAppVersionContent) {
            Write-Host "Restoring original AppVersion.cs..." -ForegroundColor Yellow
            Set-Content $appVersionFile $originalAppVersionContent -NoNewline
        }
        Write-Host "Rolled back version changes due to failure." -ForegroundColor Red
    }

    $versionUpdated = $false

    # Update version in files
    if ($newVersion -ne $currentVersion) {
        Write-Host ""
        Write-Host "[Version] Updating..." -ForegroundColor Yellow
        
        # Extract numeric version for AssemblyVersion/FileVersion (strip prerelease suffix)
        $numericVersion = $newVersion -replace '-.*$', ''
        
        # Update csproj - Version can have prerelease, AssemblyVersion/FileVersion must be numeric
        $csprojContent = $csprojContent -replace '<Version>\d+\.\d+\.\d+(-[A-Za-z0-9\.-]+)?</Version>', "<Version>$newVersion</Version>"
        $csprojContent = $csprojContent -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$numericVersion.0</AssemblyVersion>"
        $csprojContent = $csprojContent -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$numericVersion.0</FileVersion>"
        Set-Content $projectFile $csprojContent -NoNewline
        
        # Update AppVersion.cs
        if (Test-Path $appVersionFile) {
            $appVersionContent = Get-Content $appVersionFile -Raw
            $versionPattern = 'public const string Version = "[^"]+";'
            $appVersionContent = $appVersionContent -replace $versionPattern, "public const string Version = `"$newVersion`";"
            $today = Get-Date -Format "yyyy-MM-dd"
            $datePattern = 'public const string BuildDate = "[^"]+";'
            $appVersionContent = $appVersionContent -replace $datePattern, "public const string BuildDate = `"$today`";"
            
            # Add changelog entry if we have release notes
            if ($releaseNotes.Count -gt 0) {
                # Build the changelog entry (escape double quotes in notes for valid C#)
                $changelogLines = ($releaseNotes | ForEach-Object { 
                    $escaped = $_ -replace '"', '\"'
                    "            `"$escaped`""
                }) -join ",`n"
                # Determine a friendly release title based on bump intent
                $releaseTitle = 
                    if ($Major -or ($Bump -and $Bump.ToLower() -eq 'major')) { "Major Release" }
                    elseif ($Minor -or ($Bump -and $Bump.ToLower() -eq 'minor')) { "Feature Update" }
                    else { "Release Update" }
                
                $newChangelogEntry = @"
        new("$newVersion", "$today", "$releaseTitle", new[]
        {
$changelogLines
        }),
"@
                # Insert after "new List<ChangelogEntry>" opening brace
                $changelogInsertPattern = '(new List<ChangelogEntry>\s*\{)\s*\r?\n'
                if ($appVersionContent -match $changelogInsertPattern) {
                    $appVersionContent = $appVersionContent -replace $changelogInsertPattern, "`$1`n$newChangelogEntry`n"
                    Write-Host "  Added changelog entry" -ForegroundColor Green
                }
            }
            
            Set-Content $appVersionFile $appVersionContent -NoNewline
            $modifiedFiles += $appVersionFile
        }
        
        Write-Host "  Updated to v$newVersion" -ForegroundColor Green
        $versionUpdated = $true
    }
} else {
    Write-Host "Could not parse version from csproj" -ForegroundColor Red
    exit 1
}

# Build and Publish
Write-Host ""
Write-Host "[Build] Building release..." -ForegroundColor Yellow
& "$rootDir\scripts\clean.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Warning: Clean had issues, continuing..." -ForegroundColor Yellow
}
& "$rootDir\scripts\build.ps1" -Release -Clean -Restore

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    if ($versionUpdated) { Restore-OriginalFiles }
    exit 1
}

Write-Host ""
Write-Host "[Publish] Creating portable build..." -ForegroundColor Yellow
& "$rootDir\scripts\publish.ps1"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    if ($versionUpdated) { Restore-OriginalFiles }
    exit 1
}

# Verify publish output exists
$publishPortableDir = Join-Path $rootDir 'publish-portable'
if (-not (Test-Path $publishPortableDir)) {
    Write-Host ""
    Write-Host "ERROR: Publish output not found at $publishPortableDir" -ForegroundColor Red
    if ($versionUpdated) { Restore-OriginalFiles }
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Portable: $publishPortableDir" -ForegroundColor Cyan
Write-Host ""

# Git operations
if (-not $SkipGit) {
    Write-Host "[Git] Committing and tagging..." -ForegroundColor Yellow
    
    # Validate working tree state
    $dirtyFiles = git -C $rootDir status --porcelain -- ':!src/SOUP.csproj' ':!src/Core/AppVersion.cs'
    if ($dirtyFiles) {
        Write-Host "  Warning: Uncommitted changes detected outside release files:" -ForegroundColor Yellow
        $dirtyFiles | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkYellow }
        Write-Host "  These will be included in the release commit." -ForegroundColor Yellow
    }
    
    # Create release notes body for GitHub
    $notesBody = ""
    if ($releaseNotes.Count -gt 0) {
        $notesBody = ($releaseNotes | ForEach-Object { "- $_" }) -join "`n"
    }
    
    # Stage only the files we modified (plus any other tracked changes)
    foreach ($f in $modifiedFiles) {
        git -C $rootDir add $f
    }
    
    # Write commit message to temp file to preserve newlines reliably
    $commitMsgFile = Join-Path $env:TEMP "soup-release-commit-msg.txt"
    if ($releaseNotes.Count -gt 0) {
        "Release v$newVersion`n`n$notesBody" | Set-Content $commitMsgFile -Encoding utf8
    } else {
        "Release v$newVersion" | Set-Content $commitMsgFile -Encoding utf8
    }
    git -C $rootDir commit -F $commitMsgFile
    Remove-Item $commitMsgFile -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Nothing to commit" -ForegroundColor Yellow
    }
    
    Write-Host "  Pushing..." -ForegroundColor Gray
    # Determine current branch and push explicitly so we don't accidentally skip pushing to 'main'
    $currentBranch = (git -C $rootDir rev-parse --abbrev-ref HEAD).Trim()
    if ([string]::IsNullOrWhiteSpace($currentBranch) -or $currentBranch -eq 'HEAD') {
        Write-Host "  Detached HEAD detected or no branch found; defaulting to 'main'" -ForegroundColor Yellow
        $pushBranch = 'main'
    } else {
        $pushBranch = $currentBranch
    }

    # Use --set-upstream so new local branches get an upstream set on first push
    git -C $rootDir push --set-upstream origin $pushBranch
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Git push failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "  Creating tag $tagName..." -ForegroundColor Gray
    $tagMsgFile = Join-Path $env:TEMP "soup-release-tag-msg.txt"
    if ($releaseNotes.Count -gt 0) {
        "Release $tagName`n`n$notesBody" | Set-Content $tagMsgFile -Encoding utf8
    } else {
        "Release $tagName" | Set-Content $tagMsgFile -Encoding utf8
    }
    git -C $rootDir tag -a $tagName -F $tagMsgFile
    Remove-Item $tagMsgFile -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Git tag creation failed!" -ForegroundColor Red
        exit 1
    }
    
    git -C $rootDir push origin $tagName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Git tag push failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  v$newVersion Released!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "GitHub Actions will create the release with assets." -ForegroundColor White
    Write-Host "https://github.com/PossiblyPengu/SOUP/actions" -ForegroundColor Cyan
} else {
    Write-Host "Skipped git (use without -SkipGit to publish)" -ForegroundColor Gray
}

Write-Host ""
