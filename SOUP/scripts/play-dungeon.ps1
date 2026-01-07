# Launch the Friendship Dungeon game (MonoGame)
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$gameDir = Split-Path -Parent $rootDir

$projectFile = Join-Path $gameDir "FriendshipDungeonMG" "FriendshipDungeonMG.csproj"
Write-Host "Launching Friendship Dungeon (MonoGame)..." -ForegroundColor Cyan

# Find dotnet (check environment variable, then fallback to system dotnet)
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration Debug --no-build
} else {
    & $dotnetPath run --project $projectFile --configuration Debug
}
