# ============================================================================
# SOUP Tools Runner
# ============================================================================
# Usage:
#   .\scripts\tools.ps1 list               # List available tools
#   .\scripts\tools.ps1 import-dict        # Run ImportDictionary tool
#   .\scripts\tools.ps1 inspect-excel      # Run InspectExcel tool
#   .\scripts\tools.ps1 inspect-db         # Run InspectOrderDb tool
#   .\scripts\tools.ps1 build              # Build all tools
# ============================================================================

param(
    [Parameter(Position=0)]
    [ValidateSet("list", "import-dict", "inspect-excel", "inspect-db", "build", "help")]
    [string]$Command = "list",
    
    [Parameter(Position=1, ValueFromRemainingArguments)]
    [string[]]$ToolArgs
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_common.ps1"

$toolsDir = Join-Path $rootDir "tools"

$tools = @{
    "import-dict"   = @{ Name = "ImportDictionary"; Desc = "Import dictionary data from Excel" }
    "inspect-excel" = @{ Name = "InspectExcel"; Desc = "Inspect Excel file structure" }
    "inspect-db"    = @{ Name = "InspectOrderDb"; Desc = "Inspect OrderLog database" }
}

function Show-Tools {
    Write-Host ""
    Write-Host "=== Available Tools ===" -ForegroundColor Cyan
    Write-Host ""
    foreach ($key in $tools.Keys | Sort-Object) {
        $tool = $tools[$key]
        $path = Join-Path $toolsDir "$($tool.Name)\$($tool.Name).csproj"
        $exists = Test-Path $path
        
        Write-Host "  $key" -NoNewline -ForegroundColor Yellow
        Write-Host " - $($tool.Desc)" -NoNewline
        if (-not $exists) {
            Write-Host " (not found)" -ForegroundColor Red
        } else {
            Write-Host ""
        }
    }
    Write-Host ""
    Write-Host "Usage: .\scripts\tools.ps1 <tool-name> [args...]" -ForegroundColor Gray
    Write-Host ""
}

function Run-Tool($toolKey, $toolArgs) {
    if (-not $tools.ContainsKey($toolKey)) {
        Write-Host "Unknown tool: $toolKey" -ForegroundColor Red
        Show-Tools
        return
    }
    
    $tool = $tools[$toolKey]
    $projectPath = Join-Path $toolsDir "$($tool.Name)\$($tool.Name).csproj"
    
    if (-not (Test-Path $projectPath)) {
        Write-Host "Tool not found: $projectPath" -ForegroundColor Red
        return
    }
    
    Write-Host ""
    Write-Host "=== Running $($tool.Name) ===" -ForegroundColor Cyan
    Write-Host ""
    
    if ($toolArgs) {
        & $dotnetPath run --project $projectPath -- @toolArgs
    } else {
        & $dotnetPath run --project $projectPath
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Build-AllTools {
    Write-Host ""
    Write-Host "=== Building All Tools ===" -ForegroundColor Cyan
    Write-Host ""
    
    foreach ($key in $tools.Keys | Sort-Object) {
        $tool = $tools[$key]
        $projectPath = Join-Path $toolsDir "$($tool.Name)\$($tool.Name).csproj"
        
        if (Test-Path $projectPath) {
            Write-Host "Building $($tool.Name)..." -ForegroundColor Yellow
            & $dotnetPath build $projectPath --configuration Release --verbosity quiet
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  OK" -ForegroundColor Green
            } else {
                Write-Host "  FAILED" -ForegroundColor Red
            }
        }
    }
    Write-Host ""
}

switch ($Command) {
    "list" { Show-Tools }
    "help" { Show-Tools }
    "build" { Build-AllTools }
    "import-dict" { Run-Tool "import-dict" $ToolArgs }
    "inspect-excel" { Run-Tool "inspect-excel" $ToolArgs }
    "inspect-db" { Run-Tool "inspect-db" $ToolArgs }
}
