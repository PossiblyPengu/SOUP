$root = "d:\CODE\Cshp\SOUP\src"
$input = Join-Path $root 'unused_private_fields_all.txt'
$out = Join-Path $root 'unused_private_fields_candidates.txt'
if (-not (Test-Path $input)) { Write-Error "Input not found: $input"; exit 1 }
$candidates = @()
Get-Content $input | ForEach-Object {
    $name = ($_ -split ':')[0].Trim()
    if (-not $name) { return }
    $esc = [regex]::Escape($name)
    $allMatches = Select-String -Path "$root\**\*.cs" -Pattern "\\b$esc\\b" -AllMatches -ErrorAction SilentlyContinue
    # matches excluding declaration lines
    $nonDeclMatches = $allMatches | Where-Object { $_.Line -notmatch "private\s+(readonly\s+)?[^\s]+\s+$esc\b" }
    $countNonDecl = ($nonDeclMatches | Measure-Object).Count
    if ($countNonDecl -eq 0) {
        # find declaration location(s)
        $declMatches = Select-String -Path "$root\**\*.cs" -Pattern "private\s+(readonly\s+)?[^\s]+\s+$esc\b" -AllMatches -ErrorAction SilentlyContinue
        $declStrings = $declMatches | ForEach-Object { ($_.Path -replace '\\','/') + ':' + $_.LineNumber }
        $candidates += "$name -> declarations: " + ($declStrings -join ', ')
    }
}
$candidates | Out-File -FilePath $out -Encoding utf8
Write-Output "Wrote $out"
Get-Content $out | Select-Object -First 200
