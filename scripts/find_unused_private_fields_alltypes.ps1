$root = "d:\CODE\Cshp\SOUP\src"
$paths = @("$root\**\*.cs","$root\**\*.xaml")
$decls = Select-String -Path $paths[0] -Pattern 'private\s+(?:readonly\s+)?[^\s]+\s+(_[A-Za-z0-9_]+)' -AllMatches -ErrorAction SilentlyContinue | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
if (-not $decls) { Write-Output "No declarations found"; exit }
$result = @()
foreach ($d in $decls) {
  $esc = [regex]::Escape($d)
  $count = (Select-String -Path $paths -Pattern "\\b$esc\\b" -AllMatches -ErrorAction SilentlyContinue | Measure-Object).Count
  if ($count -le 1) { $result += "$d : $count" }
}
$result | Out-File -FilePath "$root\unused_private_fields_all.txt" -Encoding utf8
Write-Output "Wrote results to $root\unused_private_fields_all.txt"
Get-Content "$root\unused_private_fields_all.txt"
