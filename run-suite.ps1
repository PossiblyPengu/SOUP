<#
.SYNOPSIS
    Wrapper script to run S.A.P from the repository root.

.DESCRIPTION
    This script redirects to the main launch script in the scripts folder.
    For full options, use: .\scripts\run-suite.ps1 -Help
#>

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
& "$scriptPath\scripts\run-suite.ps1" @args
