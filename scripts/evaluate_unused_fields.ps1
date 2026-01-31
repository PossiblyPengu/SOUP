$root = "d:\CODE\Cshp\SOUP\src"
$inputFile = Join-Path $root 'unused_private_fields_all.txt'
$out = Join-Path $root 'unused_private_fields_report.txt'
if (-not (Test-Path $inputFile)) { Write-Error "Input not found: $inputFile"; exit 1 }
$result = @()
Get-Content $inputFile | ForEach-Object {
    $name = ($_ -split ':')[0].Trim()
    if (-not $name) { return }
    $esc = [regex]::Escape($name)
    $matches = Select-String -Path "$root\**\*.cs" -Pattern "\\b$esc\\b" -AllMatches -ErrorAction SilentlyContinue
    $total = ($matches | Measure-Object).Count
    # find declaration match count
    $declMatches = Select-String -Path "$root\**\*.cs" -Pattern "private\s+(readonly\s+)?[^\s]+\s+$esc\b" -AllMatches -ErrorAction SilentlyContinue
    $declCount = ($declMatches | Measure-Object).Count
    $usedElsewhere = $total - $declCount
    $entry = [PSCustomObject]@{
        Name = $name
        DeclarationCount = $declCount
        TotalReferences = $total
        UsedElsewhere = $usedElsewhere
        Declarations = ($declMatches | ForEach-Object { ($_.Path -replace '\\','/') + ':' + $_.LineNumber }) -join ", "
    }
    $result += $entry
}
$result | Sort-Object @{Expression={$_.UsedElsewhere};Descending=$true},Name | Format-Table -AutoSize | Out-String | Out-File $out -Encoding utf8
Write-Output "Wrote report to $out"
Get-Content $out | Select-Object -First 200
