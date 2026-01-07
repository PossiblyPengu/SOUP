<#
.SYNOPSIS
    Simple HTTP server for hosting SOUP updates locally.
    
.DESCRIPTION
    Starts a basic HTTP server on port 8080 to serve update files.
    Place version.json and SOUP-portable.zip in the publish folder.
    
.EXAMPLE
    .\serve-updates.ps1
    .\serve-updates.ps1 -Port 9000
#>

param(
    [int]$Port = 8080
)

$rootDir = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $rootDir "publish"

if (-not (Test-Path $publishDir)) {
    Write-Host "ERROR: Publish folder not found at $publishDir" -ForegroundColor Red
    Write-Host "Run .\scripts\publish.ps1 first." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SOUP Update Server" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Serving files from: $publishDir" -ForegroundColor White
Write-Host "URL: http://localhost:$Port/" -ForegroundColor Green
Write-Host ""
Write-Host "Files available:" -ForegroundColor Yellow
Get-ChildItem $publishDir | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
Write-Host ""
Write-Host "Press Ctrl+C to stop the server." -ForegroundColor DarkGray
Write-Host ""

# Create HTTP listener
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Prefixes.Add("http://+:$Port/")

try {
    $listener.Start()
}
catch {
    # If binding to all interfaces fails, try localhost only
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://localhost:$Port/")
    $listener.Start()
}

Write-Host "Server started. Listening on port $Port..." -ForegroundColor Green

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response
        
        $requestPath = $request.Url.LocalPath.TrimStart('/')
        if ([string]::IsNullOrEmpty($requestPath)) {
            $requestPath = "version.json"
        }
        
        $filePath = Join-Path $publishDir $requestPath
        
        $timestamp = Get-Date -Format "HH:mm:ss"
        
        if (Test-Path $filePath -PathType Leaf) {
            $content = [System.IO.File]::ReadAllBytes($filePath)
            
            # Set content type
            $extension = [System.IO.Path]::GetExtension($filePath).ToLower()
            $contentType = switch ($extension) {
                ".json" { "application/json" }
                ".zip" { "application/zip" }
                ".exe" { "application/octet-stream" }
                default { "application/octet-stream" }
            }
            
            $response.ContentType = $contentType
            $response.ContentLength64 = $content.Length
            $response.Headers.Add("Access-Control-Allow-Origin", "*")
            $response.OutputStream.Write($content, 0, $content.Length)
            $response.StatusCode = 200
            
            Write-Host "[$timestamp] 200 $requestPath ($($content.Length) bytes)" -ForegroundColor Green
        }
        else {
            $response.StatusCode = 404
            $errorBytes = [System.Text.Encoding]::UTF8.GetBytes("File not found: $requestPath")
            $response.OutputStream.Write($errorBytes, 0, $errorBytes.Length)
            
            Write-Host "[$timestamp] 404 $requestPath" -ForegroundColor Red
        }
        
        $response.Close()
    }
}
finally {
    $listener.Stop()
    Write-Host "Server stopped." -ForegroundColor Yellow
}
