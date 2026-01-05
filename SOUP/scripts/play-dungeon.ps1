# Launch the Friendship Dungeon game
# Use -MonoGame switch to run the MonoGame version (better graphics)
param(
    [switch]$NoBuild,
    [switch]$MonoGame
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$gameDir = Split-Path -Parent $rootDir

if ($MonoGame) {
    $projectFile = Join-Path $gameDir "FriendshipDungeonMG" "FriendshipDungeonMG.csproj"
    Write-Host "Launching Friendship Dungeon (MonoGame)..." -ForegroundColor Cyan
} else {
    $projectFile = Join-Path $gameDir "FriendshipDungeon" "FriendshipDungeon.csproj"
    Write-Host "Launching Friendship Dungeon (WPF)..." -ForegroundColor DarkMagenta
}

# Find dotnet (check environment variable, then fallback to system dotnet)
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

if ($NoBuild) {
    & $dotnetPath run --project $projectFile --configuration Debug --no-build
} else {
    & $dotnetPath run --project $projectFile --configuration Debug
}
