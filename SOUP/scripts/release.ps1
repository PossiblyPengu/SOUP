# ============================================================================
# SOUP Release Script (GitHub + Local Publishing)
# ============================================================================
# Usage:
#   .\scripts\release.ps1                  # Interactive version bump + release
#   .\scripts\release.ps1 -Patch           # Bump patch version (4.6.2 -> 4.6.3)
#   .\scripts\release.ps1 -Minor           # Bump minor version (4.6.2 -> 4.7.0)
#   .\scripts\release.ps1 -Major           # Bump major version (4.6.2 -> 5.0.0)
#   .\scripts\release.ps1 -Version 5.0.0   # Set specific version
#   .\scripts\release.ps1 -DryRun          # Preview without making changes
#   .\scripts\release.ps1 -SkipGit         # Build only, don't commit/tag/push
#   .\scripts\release.ps1 -Notes "note"    # Single-line release note
# ============================================================================

param(
    [switch]$Patch,
    [switch]$Minor,
    [switch]$Major,
    [string]$Version,
    [switch]$DryRun,
    [switch]$SkipGit,
    [string]$Notes
)

$ErrorActionPreference = "Stop"

# Configuration
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"
$csprojFile = Join-Path $rootDir "src\SOUP.csproj"

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  SOUP Release" -ForegroundColor Magenta
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
            Write-Host "ERROR: Invalid version format. Use X.Y.Z" -ForegroundColor Red
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
        Write-Host "Select version bump:" -ForegroundColor Yellow
        Write-Host "  [1] Patch  ($majorNum.$minorNum.$($patchNum + 1))"
        Write-Host "  [2] Minor  ($majorNum.$($minorNum + 1).0)"
        Write-Host "  [3] Major  ($($majorNum + 1).0.0)"
        Write-Host "  [Q] Quit"
        Write-Host ""
        
        $choice = Read-Host "Choice"
        
        switch ($choice) {
            "1" { $newVersion = "$majorNum.$minorNum.$($patchNum + 1)" }
            "2" { $newVersion = "$majorNum.$($minorNum + 1).0" }
            "3" { $newVersion = "$($majorNum + 1).0.0" }
            "q" { exit 0 }
            "Q" { exit 0 }
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
        Write-Host "  Would update: $currentVersion -> $newVersion" -ForegroundColor Gray
        exit 0
    }
    
    # Update version in files
    if ($newVersion -ne $currentVersion) {
        Write-Host ""
        Write-Host "[Version] Updating..." -ForegroundColor Yellow
        
        # Update csproj
        $csprojContent = $csprojContent -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>"
        $csprojContent = $csprojContent -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newVersion.0</AssemblyVersion>"
        $csprojContent = $csprojContent -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newVersion.0</FileVersion>"
        Set-Content $csprojFile $csprojContent -NoNewline
        
        # Update AppVersion.cs
        $appVersionFile = Join-Path $srcDir "Core\AppVersion.cs"
        if (Test-Path $appVersionFile) {
            $appVersionContent = Get-Content $appVersionFile -Raw
            $versionPattern = 'public const string Version = "[^"]+";'
            $appVersionContent = $appVersionContent -replace $versionPattern, "public const string Version = `"$newVersion`";"
            $today = Get-Date -Format "yyyy-MM-dd"
            $datePattern = 'public const string BuildDate = "[^"]+";'
            $appVersionContent = $appVersionContent -replace $datePattern, "public const string BuildDate = `"$today`";"
            
            # Add changelog entry if we have release notes
            if ($releaseNotes.Count -gt 0) {
                # Build the changelog entry
                $changelogLines = ($releaseNotes | ForEach-Object { "            `"$_`"" }) -join ",`n"
                $releaseTitle = if ($Major) { "Major Release" } elseif ($Minor) { "Feature Update" } else { "Release Update" }
                
                $newChangelogEntry = @"
        new("$newVersion", "$today", "$releaseTitle", new[]
        {
$changelogLines
        }),
"@
                # Insert after "new List<ChangelogEntry>" opening
                $changelogInsertPattern = '(public static IReadOnlyList<ChangelogEntry> Changelog \{ get; \} = new List<ChangelogEntry>\s*\{)\s*\n'
                if ($appVersionContent -match $changelogInsertPattern) {
                    $appVersionContent = $appVersionContent -replace $changelogInsertPattern, "`$1`n$newChangelogEntry`n"
                    Write-Host "  Added changelog entry" -ForegroundColor Green
                }
            }
            
            Set-Content $appVersionFile $appVersionContent -NoNewline
        }
        
        Write-Host "  Updated to v$newVersion" -ForegroundColor Green
    }
} else {
    Write-Host "Could not parse version from csproj" -ForegroundColor Red
    exit 1
}

# Build and Publish
Write-Host ""
Write-Host "[Build] Building release..." -ForegroundColor Yellow
& "$rootDir\scripts\clean.ps1" -All 2>$null
& "$rootDir\scripts\build.ps1" -Release -Clean -Restore

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[Publish] Creating portable build..." -ForegroundColor Yellow
& "$rootDir\scripts\publish.ps1"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Portable: $(Join-Path $rootDir 'publish-portable')" -ForegroundColor Cyan
Write-Host ""

# Git operations
if (-not $SkipGit) {
    Write-Host "[Git] Committing and tagging..." -ForegroundColor Yellow
    
    # Create release notes body for GitHub
    $notesBody = ""
    if ($releaseNotes.Count -gt 0) {
        $notesBody = ($releaseNotes | ForEach-Object { "- $_" }) -join "`n"
    }
    
    git -C $rootDir add -A
    
    # Commit with notes in message if provided
    if ($releaseNotes.Count -gt 0) {
        $commitMsg = "Release v$newVersion`n`n$notesBody"
        git -C $rootDir commit -m $commitMsg
    } else {
        git -C $rootDir commit -m "Release v$newVersion"
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Nothing to commit" -ForegroundColor Yellow
    }
    
    Write-Host "  Pushing..." -ForegroundColor Gray
    git -C $rootDir push
    
    Write-Host "  Creating tag $tagName..." -ForegroundColor Gray
    if ($releaseNotes.Count -gt 0) {
        $tagMsg = "Release $tagName`n`n$notesBody"
        git -C $rootDir tag -a $tagName -m $tagMsg
    } else {
        git -C $rootDir tag -a $tagName -m "Release $tagName"
    }
    git -C $rootDir push origin $tagName
    
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
