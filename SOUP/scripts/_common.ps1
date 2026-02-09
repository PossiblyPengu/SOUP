# ============================================================================
# SOUP Shared Setup (dot-source this from other scripts)
# ============================================================================
# Usage: . "$PSScriptRoot\_common.ps1"
#
# Provides:
#   $rootDir      - SOUP repository root
#   $srcDir       - Source directory (src/)
#   $projectFile  - Path to SOUP.csproj
#   $dotnetPath   - Resolved dotnet executable
#
# Also configures:
#   $env:DOTNET_ROOT and $env:PATH for local SDK discovery
# ============================================================================

# Resolve repository paths
$script:rootDir     = Split-Path -Parent $PSScriptRoot
$script:srcDir      = Join-Path $rootDir "src"
$script:projectFile = Join-Path $srcDir "SOUP.csproj"

# Setup local .NET SDK environment
# Prefer LOCAL_DOTNET_ROOT env var, then auto-discover from known folder
$localSdkRoot = if ($env:LOCAL_DOTNET_ROOT) { $env:LOCAL_DOTNET_ROOT } else { "D:\CODE\important files" }
$localSDKPath = $null
if ($localSdkRoot -and (Test-Path $localSdkRoot)) {
    if (Test-Path (Join-Path $localSdkRoot "dotnet.exe")) {
        $localSDKPath = $localSdkRoot
    } else {
        $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'dotnet-sdk-10*' } |
            Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue
        if (-not $localSDKPath) {
            $localSDKPath = Get-ChildItem -Path $localSdkRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like 'dotnet-sdk*' } |
                Select-Object -First 1 -ExpandProperty FullName -ErrorAction SilentlyContinue
        }
    }
}
if ($localSDKPath -and (Test-Path $localSDKPath)) {
    $env:DOTNET_ROOT = $localSDKPath
    $env:PATH = "$localSDKPath;$env:PATH"
}

# Resolve dotnet executable
$script:dotnetPath = if ($env:DOTNET_PATH -and (Test-Path $env:DOTNET_PATH)) { $env:DOTNET_PATH } else { "dotnet" }
