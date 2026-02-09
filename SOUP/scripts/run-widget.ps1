# ============================================================================
# SOUP Run OrderLog Widget (Standalone)
# ============================================================================
# Usage:
#   .\scripts\run-widget.ps1              # Build and run widget
#   .\scripts\run-widget.ps1 -NoBuild     # Run without building
# ============================================================================

param([switch]$NoBuild)

$splat = @{
    Module      = "widget"
    DisplayName = "OrderLog Widget (Standalone)"
    Color       = "Green"
    ExtraArgs   = @("--widget")
}
if ($NoBuild) { $splat.NoBuild = $true }

& "$PSScriptRoot\run-module.ps1" @splat
