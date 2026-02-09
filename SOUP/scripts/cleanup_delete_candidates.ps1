# Cleanup generated artifacts and test files (irreversible)
$rootDir = Split-Path -Parent $PSScriptRoot

$paths = @(
    (Join-Path $rootDir 'publish'),
    (Join-Path $rootDir 'publish-framework'),
    (Join-Path $rootDir 'publish-portable'),
    (Join-Path $rootDir 'src\bin\Release'),
    (Join-Path $rootDir 'src\unused_private_fields.txt'),
    (Join-Path $rootDir 'src\unused_private_fields_all.txt'),
    (Join-Path $rootDir 'src\unused_private_fields_candidates.txt'),
    (Join-Path $rootDir 'src\unused_private_fields_report.txt'),
    (Join-Path $rootDir 'src\vetted_unused_private_fields_full.csv'),
    (Join-Path $rootDir 'src\vetted_unused_private_fields_top30.csv'),
    'D:\CODE\Cshp\test_files'
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Output "Removing: $p"
        Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction Continue
    }
    else {
        Write-Output "Not found: $p"
    }
}

Write-Output "`nCleanup finished. Remaining paths status:"
foreach ($p in $paths) { Write-Output "$p -> $(Test-Path $p)" }
