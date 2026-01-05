# Play Friendship Dungeon
# -MonoGame : Run the MonoGame version (better graphics, recommended)
# -WPF     : Run the WPF version (original)
param(
    [switch]$MonoGame,
    [switch]$WPF
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Default to MonoGame if neither is specified
if (-not $MonoGame -and -not $WPF) {
    $MonoGame = $true
}

# Find dotnet
$dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }

Write-Host ""
Write-Host "  + FRIENDSHIP DUNGEON +  " -ForegroundColor Magenta
Write-Host "  A silly-creepy adventure  " -ForegroundColor DarkMagenta
Write-Host ""

if ($MonoGame) {
    $projectFile = Join-Path $scriptDir "FriendshipDungeonMG" "FriendshipDungeonMG.csproj"
    Write-Host "  [MonoGame Version]" -ForegroundColor Cyan
} else {
    $projectFile = Join-Path $scriptDir "FriendshipDungeon" "FriendshipDungeon.csproj"
    Write-Host "  [WPF Version]" -ForegroundColor Yellow
}

Write-Host ""
& $dotnetPath run --project $projectFile --configuration Debug
