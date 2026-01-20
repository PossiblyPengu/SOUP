# Generates a vetted CSV from src/unused_private_fields_report.txt
$report = "$(Split-Path -Parent $PSScriptRoot)\src\unused_private_fields_report.txt"
$out = "$(Split-Path -Parent $PSScriptRoot)\src\vetted_unused_private_fields_full.csv"
if (-not (Test-Path $report)) { Write-Error "Report not found: $report"; exit 1 }

$lines = Get-Content $report

$results = @()
foreach ($line in $lines) {
    if ($line -match '^\s*(\S+)\s+(\d+)\s+(-?\d+)\s+(-?\d+)\s+(.*)$') {
        $name = $matches[1]
        $declCount = [int]$matches[2]
        $totalRefs = [int]$matches[3]
        $usedElsewhere = [int]$matches[4]
        $declaration = $matches[5].Trim()

        $recommendation = if ($totalRefs -eq 0) { 'Candidate - manual verify (XAML/reflection)' } else { 'Keep' }

        $results += [pscustomobject]@{
            Field = $name
            Declaration = $declaration
            DeclarationCount = $declCount
            TotalReferences = $totalRefs
            UsedElsewhere = $usedElsewhere
            Recommendation = $recommendation
        }
    }
}

$results | Sort-Object Recommendation,Field | Export-Csv -NoTypeInformation -Path $out -Encoding UTF8
Write-Output "Generated $out with $($results.Count) entries."
