param(
    [string]$RepoRoot = "D:\CODE\Cshp",
    [string]$OutDir = "D:\CODE\Cshp\EssentialsBuddyApp\src"
)

$items = @(
    "BusinessToolsSuite\src\BusinessToolsSuite.Core",
    "BusinessToolsSuite\src\BusinessToolsSuite.Infrastructure",
    "BusinessToolsSuite\src\BusinessToolsSuite.Shared",
    "BusinessToolsSuite\src\Features\BusinessToolsSuite.Features.EssentialsBuddy",
    "BusinessToolsSuite\src\BusinessToolsSuite.Tools\ImportTester"
)

Write-Host "Exporting Essentials Buddy related projects from $RepoRoot to $OutDir"

if (-Not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

foreach ($item in $items) {
    $src = Join-Path $RepoRoot $item
    if (-Not (Test-Path $src)) {
        Write-Warning "Source not found: $src"
        continue
    }
    $dest = Join-Path $OutDir (Split-Path $item -Leaf)
    Write-Host "Copying $src -> $dest"
    $robocopy = Join-Path $env:WINDIR "system32\robocopy.exe"
    if (Test-Path $robocopy) {
        & $robocopy $src $dest /MIR /NFL /NDL /NJH /NJS | Out-Null
    } else {
        Copy-Item -Path $src -Destination $dest -Recurse -Force
    }
}

# Normalize ProjectReference paths and auto-insert logging package for Infrastructure
Write-Host "Rewriting ProjectReference paths in copied csproj files under $OutDir"
$csprojFiles = Get-ChildItem -Path $OutDir -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue
foreach ($f in $csprojFiles) {
    $full = $f.FullName
    try {
        $text = Get-Content -Path $full -Raw -ErrorAction Stop
    } catch {
        Write-Warning ("Failed to read {0}: {1}" -f $($full), $($_.Exception.Message))
        continue
    }

    $pattern = '(?:\.{2}[\\/]){1,}BusinessToolsSuite'
    $replacement = '..\\BusinessToolsSuite'
    $new = [regex]::Replace($text, $pattern, $replacement)

    if ($new -eq $text) {
        $new = $text -replace '\.\./BusinessToolsSuite','..\\BusinessToolsSuite'
        $new = $new -replace '\.\.\\BusinessToolsSuite','..\\BusinessToolsSuite'
    }

    if ($full -match 'BusinessToolsSuite.Infrastructure\\.*\.csproj') {
        if ($new -notmatch 'Microsoft.Extensions.Logging.Abstractions') {
            $addBlock = @'
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

'@
            $new = $new -replace '(?=</Project>)', $addBlock
        }
    }

    if ($new -ne $text) {
        Set-Content -Path $full -Value $new
        Write-Host ("Rewrote project references / added packages in: {0}" -f $($full))
    }
}

# Create solution and add projects
$solutionDir = Split-Path $OutDir -Parent
$solutionPath = Join-Path $solutionDir "EssentialsBuddyApp.sln"
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host "Creating solution at $solutionPath"
    dotnet new sln -o $solutionDir -n EssentialsBuddyApp --force | Out-Null

    Get-ChildItem -Path $OutDir -Directory | ForEach-Object {
        $projFiles = Get-ChildItem -Path $_.FullName -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue
        foreach ($p in $projFiles) {
            Write-Host "Adding project $($p.FullName) to solution"
            dotnet sln $solutionPath add $p.FullName | Out-Null
        }
    }
}

Write-Host "Export complete. Open $solutionPath to build the copied projects."