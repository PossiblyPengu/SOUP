# Play Friendship Dungeon (MonoGame)
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find dotnet
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

Write-Host ""
Write-Host "  + FRIENDSHIP DUNGEON +  " -ForegroundColor Magenta
Write-Host "  A silly-creepy adventure  " -ForegroundColor DarkMagenta
Write-Host ""

$projectFile = Join-Path (Join-Path $scriptDir "FriendshipDungeonMG") "FriendshipDungeonMG.csproj"

if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration Debug --no-build
} else {
    & $dotnetPath run --project $projectFile --configuration Debug
}
