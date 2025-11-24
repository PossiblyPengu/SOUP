param(
    [string]$RepoRoot = "D:\CODE\Cshp",
    [string]$OutDir = "D:\CODE\Cshp\AllocationBuddyApp\src"
)

$items = @(
    "BusinessToolsSuite\src\BusinessToolsSuite.Core",
    "BusinessToolsSuite\src\BusinessToolsSuite.Infrastructure",
    "BusinessToolsSuite\src\BusinessToolsSuite.Shared",
    "BusinessToolsSuite\src\Features\BusinessToolsSuite.Features.AllocationBuddy",
    "BusinessToolsSuite\src\BusinessToolsSuite.Tools\ImportTester"
)

Write-Host "Exporting Allocation Buddy related projects from $RepoRoot to $OutDir"

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
    # Use Robocopy for robust copying if available
    $robocopy = Join-Path $env:WINDIR "system32\robocopy.exe"
    if (Test-Path $robocopy) {
        & $robocopy $src $dest /MIR /NFL /NDL /NJH /NJS | Out-Null
    } else {
        Copy-Item -Path $src -Destination $dest -Recurse -Force
    }
}

# Normalize ProjectReference paths in copied .csproj files so the exported tree is standalone
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

    # Replace any leading repeated ../ or ..\ occurrences before BusinessToolsSuite with a single ..\
    # Handles ../../BusinessToolsSuite, ..\..\BusinessToolsSuite, ../../../BusinessToolsSuite, etc.
    $pattern = '(?:\.{2}[\\/]){1,}BusinessToolsSuite'
    $replacement = '..\\BusinessToolsSuite'
    $new = [regex]::Replace($text, $pattern, $replacement)

    # Also normalize any remaining '../BusinessToolsSuite' or '..\BusinessToolsSuite' to use backslash
    if ($new -eq $text) {
        $new = $text -replace '\.\./BusinessToolsSuite','..\\BusinessToolsSuite'
        $new = $new -replace '\.\.\\BusinessToolsSuite','..\\BusinessToolsSuite'
    }

    # Auto-add known missing package references for Infrastructure project in the export
    if ($full -match 'BusinessToolsSuite.Infrastructure\\.*\.csproj') {
        if ($new -notmatch 'Microsoft.Extensions.Logging.Abstractions') {
                        # Insert a small ItemGroup with the package before closing </Project>
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


# Create a solution and add projects if dotnet is available
$solutionDir = Split-Path $OutDir -Parent
$solutionPath = Join-Path $solutionDir "AllocationBuddyApp.sln"
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host "Creating solution at $solutionPath"
    # Force overwrite if solution exists so re-running the script is non-interactive
    dotnet new sln -o $solutionDir -n AllocationBuddyApp --force | Out-Null

    # Add projects found in dest
    Get-ChildItem -Path $OutDir -Directory | ForEach-Object {
        $projFiles = Get-ChildItem -Path $_.FullName -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue
        foreach ($p in $projFiles) {
            Write-Host "Adding project $($p.FullName) to solution"
            dotnet sln $solutionPath add $p.FullName | Out-Null
        }
    }
}

Write-Host "Export complete. Open $solutionPath to build the copied projects."
Write-Host "Note: You may need to adjust project references or package versions for standalone builds."
