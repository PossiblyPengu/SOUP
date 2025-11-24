# PowerShell script to force use of portable .NET SDK only
$env:DOTNET_ROOT = "d:\CODE\important files\dotnet-sdk-9.0.306-win-x64"
$env:PATH = "d:\CODE\important files\dotnet-sdk-9.0.306-win-x64" + ";" + $env:PATH
& "d:\CODE\important files\dotnet-sdk-9.0.306-win-x64\dotnet" run --project BusinessToolsSuite\src\BusinessToolsSuite.Desktop\BusinessToolsSuite.Desktop.csproj