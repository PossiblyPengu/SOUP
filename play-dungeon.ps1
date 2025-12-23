Write-Host "Starting SUPER HAPPY FUN ZONE..." -ForegroundColor Magenta
Write-Host ""

Set-Location SOUP\src
& "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe" run --project SOUP.csproj -- --dungeon

Write-Host ""
Write-Host "Game closed." -ForegroundColor Yellow
Read-Host "Press Enter to exit"
