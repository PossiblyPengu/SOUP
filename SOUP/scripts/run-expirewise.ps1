# ============================================================================
# SOUP Run ExpireWise Module
# ============================================================================
# Usage:
#   .\scripts\run-expirewise.ps1              # Build and run ExpireWise
#   .\scripts\run-expirewise.ps1 -NoBuild     # Run without building
# ============================================================================

param([switch]$NoBuild)

$splat = @{
    Module      = "expirewise"
    DisplayName = "ExpireWise"
    Color       = "Yellow"
}
if ($NoBuild) { $splat.NoBuild = $true }

& "$PSScriptRoot\run-module.ps1" @splat
