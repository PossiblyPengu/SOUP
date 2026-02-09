# ============================================================================
# SOUP Check Unused Counts - Quick reference count for suspected unused fields
# ============================================================================
# Usage: .\scripts\check_unused_counts.ps1
# ============================================================================

$names = '_analytics','_autoSaveIntervalMinutes','_availableMonths','_availableStores','_averageShelfLife','_bcTestResult','_bcTestSuccess','_binCode','_canAddSkuToItem','_category','_closeToTray','_confirmBeforeExit'
foreach($n in $names){
    $c = (Select-String -Path (Join-Path $PSScriptRoot '..\src\**\*.cs') -Pattern $n -SimpleMatch | Measure-Object).Count
    Write-Output "$n : $c"
}