# ============================================================================
# SOUP Security Check Script
# ============================================================================
# Run this before committing to check for potential secrets
# Usage: .\scripts\security-check.ps1
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Security Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $rootDir "src"

# Patterns that might indicate secrets (case-insensitive)
$secretPatterns = @(
    # Connection strings
    'Server=.*Password=',
    'Data Source=.*Password=',
    'mongodb(\+srv)?://[^@]+:[^@]+@',
    'mysql://[^@]+:[^@]+@',
    
    # API Keys (common formats)
    'api[_-]?key\s*[=:]\s*[''"][A-Za-z0-9_\-]{20,}[''"]',
    'apikey\s*[=:]\s*[''"][A-Za-z0-9_\-]{20,}[''"]',
    
    # Azure/BC secrets
    'client[_-]?secret\s*[=:]\s*[''"][A-Za-z0-9_\-~.]{20,}[''"]',
    
    # Generic secrets
    'password\s*[=:]\s*[''"][^''"]{8,}[''"]',
    'secret\s*[=:]\s*[''"][^''"]{16,}[''"]',
    
    # Bearer tokens
    'bearer\s+[A-Za-z0-9_\-\.]+',
    
    # Private keys
    '-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----'
)

# Files to exclude from scanning
$excludePatterns = @(
    '*.dll', '*.exe', '*.pdb', '*.xml',
    'bin/*', 'obj/*', 'publish/*',
    '.git/*', 'node_modules/*'
)

$issues = @()
$filesChecked = 0

Write-Host "Scanning source files for potential secrets..." -ForegroundColor Yellow
Write-Host ""

# Get all text files in src
$files = Get-ChildItem -Path $srcDir -Recurse -File -Include @("*.cs", "*.xaml", "*.json", "*.config", "*.xml", "*.yml", "*.yaml") |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

foreach ($file in $files) {
    $filesChecked++
    $relativePath = $file.FullName.Substring($rootDir.Length + 1)
    
    try {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        $lineNum = 0
        foreach ($line in (Get-Content -Path $file.FullName)) {
            $lineNum++
            
            foreach ($pattern in $secretPatterns) {
                if ($line -match $pattern) {
                    # Skip if it's clearly a placeholder or example
                    if ($line -match 'YOUR_|REPLACE_|example|placeholder|<.*>|\$\{|\{\{') {
                        continue
                    }
                    
                    # Skip encrypted values (DPAPI base64)
                    if ($line -match 'Encrypted\s*[=:]\s*[''"]' -or $line -match 'DPAPI') {
                        continue
                    }
                    
                    # Skip string interpolation building connection strings from variables
                    if ($line -match '\$".*\{[A-Za-z]+\}.*Password=\{[A-Za-z]+\}') {
                        continue
                    }
                    
                    $issues += [PSCustomObject]@{
                        File = $relativePath
                        Line = $lineNum
                        Pattern = $pattern
                        Content = $line.Trim().Substring(0, [Math]::Min(80, $line.Trim().Length))
                    }
                }
            }
        }
    }
    catch {
        # Skip files that can't be read
    }
}

Write-Host "Files checked: $filesChecked" -ForegroundColor Gray
Write-Host ""

if ($issues.Count -gt 0) {
    Write-Host "⚠️  POTENTIAL SECRETS FOUND!" -ForegroundColor Red
    Write-Host ""
    
    foreach ($issue in $issues) {
        Write-Host "  File: $($issue.File):$($issue.Line)" -ForegroundColor Yellow
        Write-Host "  Match: $($issue.Content)..." -ForegroundColor Gray
        Write-Host ""
    }
    
    Write-Host "Please review these matches before committing." -ForegroundColor Red
    Write-Host "If they are false positives, the patterns may need adjustment." -ForegroundColor Gray
    exit 1
}
else {
    Write-Host "✅ No potential secrets detected!" -ForegroundColor Green
    Write-Host ""
}

# Also check for files that shouldn't be committed
Write-Host "Checking for sensitive file types..." -ForegroundColor Yellow

$sensitiveFiles = Get-ChildItem -Path $rootDir -Recurse -File -Include @(
    "*.env", ".env.*", "secrets.json", "credentials.json",
    "*.pem", "*.key", "*.pfx", "*.p12",
    "external_config.json", "bc_config.json"
) -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git)\\' }

if ($sensitiveFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "⚠️  SENSITIVE FILES FOUND!" -ForegroundColor Red
    foreach ($file in $sensitiveFiles) {
        $relativePath = $file.FullName.Substring($rootDir.Length + 1)
        Write-Host "  - $relativePath" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Make sure these are in .gitignore!" -ForegroundColor Red
    exit 1
}
else {
    Write-Host "✅ No sensitive files found outside ignored paths." -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Security Check Passed" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

exit 0