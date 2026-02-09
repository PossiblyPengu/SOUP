# ============================================================================
# SOUP Run EssentialsBuddy Module
# ============================================================================
# Usage:
#   .\scripts\run-essentials.ps1              # Build and run EssentialsBuddy
#   .\scripts\run-essentials.ps1 -NoBuild     # Run without building
# ============================================================================

param([switch]$NoBuild)

$splat = @{
    Module      = "essentials"
    DisplayName = "EssentialsBuddy"
    Color       = "Magenta"
}
if ($NoBuild) { $splat.NoBuild = $true }

& "$PSScriptRoot\run-module.ps1" @splat
