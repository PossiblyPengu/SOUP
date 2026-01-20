$root = "d:\CODE\Cshp\SOUP\src"
$file = Join-Path $root 'unused_private_fields_all.txt'
if (-not (Test-Path $file)) { Write-Error "File not found: $file"; exit 1 }
Get-Content $file | ForEach-Object {
    $name = ($_ -split ':')[0].Trim()
    if (-not $name) { return }
    $pattern = "private\s+(readonly\s+)?[^\s]+\s+" + [regex]::Escape($name) + "\b"
    $matches = Select-String -Path "$root\**\*.cs" -Pattern $pattern -AllMatches -ErrorAction SilentlyContinue
    if ($matches) {
        foreach ($m in $matches) {
            Write-Output ("$name -> " + ($m.Path -replace '\\','/') + ":" + $m.LineNumber)
        }
    } else {
        Write-Output ("$name -> declaration not found")
    }
}
