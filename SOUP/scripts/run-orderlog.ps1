# ============================================================================
# SOUP Run OrderLog Module
# ============================================================================
# Usage:
#   .\scripts\run-orderlog.ps1              # Build and run OrderLog
#   .\scripts\run-orderlog.ps1 -NoBuild     # Run without building
# ============================================================================

param([switch]$NoBuild)

$splat = @{
    Module      = "orderlog"
    DisplayName = "OrderLog"
    Color       = "Green"
}
if ($NoBuild) { $splat.NoBuild = $true }

& "$PSScriptRoot\run-module.ps1" @splat
