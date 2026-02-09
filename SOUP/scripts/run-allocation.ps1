# ============================================================================
# SOUP Run AllocationBuddy Module
# ============================================================================
# Usage:
#   .\scripts\run-allocation.ps1              # Build and run AllocationBuddy
#   .\scripts\run-allocation.ps1 -NoBuild     # Run without building
# ============================================================================

param([switch]$NoBuild)

$splat = @{
    Module      = "allocation"
    DisplayName = "AllocationBuddy"
    Color       = "Blue"
}
if ($NoBuild) { $splat.NoBuild = $true }

& "$PSScriptRoot\run-module.ps1" @splat
