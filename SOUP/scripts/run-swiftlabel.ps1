# ============================================================================
# SOUP Run SwiftLabel Module
# ============================================================================
# Usage:
#   .\scripts\run-swiftlabel.ps1              # Build and run SwiftLabel
#   .\scripts\run-swiftlabel.ps1 -NoBuild     # Run without building
# ============================================================================

param([switch]$NoBuild)

$splat = @{
    Module      = "swiftlabel"
    DisplayName = "SwiftLabel"
    Color       = "DarkCyan"
}
if ($NoBuild) { $splat.NoBuild = $true }

& "$PSScriptRoot\run-module.ps1" @splat
