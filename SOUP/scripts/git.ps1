# ============================================================================
# SOUP Git Status & Helpers
# ============================================================================
# Usage:
#   .\scripts\git.ps1 status               # Show status with stats
#   .\scripts\git.ps1 changes              # Show changed files summary
#   .\scripts\git.ps1 diff                 # Show diff of staged changes
#   .\scripts\git.ps1 log                  # Show recent commits
#   .\scripts\git.ps1 branch               # Show current branch info
#   .\scripts\git.ps1 stash                # Stash current changes
#   .\scripts\git.ps1 unstash              # Pop last stash
# ============================================================================

param(
    [Parameter(Position=0)]
    [ValidateSet("status", "changes", "diff", "log", "branch", "stash", "unstash", "help")]
    [string]$Command = "status"
)

$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Push-Location $rootDir

function Show-Help {
    Write-Host ""
    Write-Host "SOUP Git Helpers" -ForegroundColor Cyan
    Write-Host "================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  status    " -NoNewline -ForegroundColor Yellow; Write-Host "Show status with file counts"
    Write-Host "  changes   " -NoNewline -ForegroundColor Yellow; Write-Host "Summarize changed files by type"
    Write-Host "  diff      " -NoNewline -ForegroundColor Yellow; Write-Host "Show diff of staged changes"
    Write-Host "  log       " -NoNewline -ForegroundColor Yellow; Write-Host "Show last 10 commits"
    Write-Host "  branch    " -NoNewline -ForegroundColor Yellow; Write-Host "Show branch info"
    Write-Host "  stash     " -NoNewline -ForegroundColor Yellow; Write-Host "Stash current changes"
    Write-Host "  unstash   " -NoNewline -ForegroundColor Yellow; Write-Host "Pop last stash"
    Write-Host ""
}

try {
    switch ($Command) {
        "status" {
            Write-Host ""
            Write-Host "=== Git Status ===" -ForegroundColor Cyan
            Write-Host ""
            git status --short
            Write-Host ""
            
            $staged = (git diff --cached --numstat | Measure-Object).Count
            $unstaged = (git diff --numstat | Measure-Object).Count
            $untracked = (git ls-files --others --exclude-standard | Measure-Object).Count
            
            Write-Host "Staged:    $staged file(s)" -ForegroundColor Green
            Write-Host "Unstaged:  $unstaged file(s)" -ForegroundColor Yellow
            Write-Host "Untracked: $untracked file(s)" -ForegroundColor Gray
            Write-Host ""
        }
        "changes" {
            Write-Host ""
            Write-Host "=== Changed Files by Type ===" -ForegroundColor Cyan
            Write-Host ""
            
            $files = git status --short | ForEach-Object { $_.Substring(3) }
            $byExt = $files | ForEach-Object { 
                $ext = [System.IO.Path]::GetExtension($_)
                if (-not $ext) { $ext = "(no ext)" }
                $ext
            } | Group-Object | Sort-Object Count -Descending
            
            $byExt | ForEach-Object {
                Write-Host "  $($_.Count.ToString().PadLeft(3)) " -NoNewline -ForegroundColor Yellow
                Write-Host $_.Name
            }
            Write-Host ""
        }
        "diff" {
            Write-Host ""
            Write-Host "=== Staged Changes ===" -ForegroundColor Cyan
            git diff --cached --stat
            Write-Host ""
        }
        "log" {
            Write-Host ""
            Write-Host "=== Recent Commits ===" -ForegroundColor Cyan
            Write-Host ""
            git log --oneline -10 --decorate
            Write-Host ""
        }
        "branch" {
            Write-Host ""
            Write-Host "=== Branch Info ===" -ForegroundColor Cyan
            Write-Host ""
            $current = git branch --show-current
            Write-Host "Current: " -NoNewline -ForegroundColor Gray
            Write-Host $current -ForegroundColor Green
            Write-Host ""
            Write-Host "All branches:" -ForegroundColor Gray
            git branch -a
            Write-Host ""
        }
        "stash" {
            Write-Host "Stashing changes..." -ForegroundColor Yellow
            git stash push -m "Auto-stash $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
            Write-Host "Done!" -ForegroundColor Green
        }
        "unstash" {
            Write-Host "Popping last stash..." -ForegroundColor Yellow
            git stash pop
            Write-Host "Done!" -ForegroundColor Green
        }
        "help" {
            Show-Help
        }
    }
} finally {
    Pop-Location
}
