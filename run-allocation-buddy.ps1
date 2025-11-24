# PowerShell script to run the published Allocation Buddy app
$exePath = "D:\CODE\Cshp\BusinessToolsSuite\src\BusinessToolsSuite.Desktop\bin\Release\net8.0\win-x64\publish\BusinessToolsSuite.Desktop.exe"
if (Test-Path $exePath) {
    Write-Host "Launching Allocation Buddy..."
    Start-Process $exePath
} else {
    Write-Host "Executable not found: $exePath"
}
