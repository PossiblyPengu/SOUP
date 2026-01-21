# Cleanup generated artifacts and test files (irreversible)
$paths = @(
 'D:\CODE\Cshp\SOUP\publish',
 'D:\CODE\Cshp\SOUP\publish-framework',
 'D:\CODE\Cshp\SOUP\publish-portable',
 'D:\CODE\Cshp\SOUP\src\bin\Release',
 'D:\CODE\Cshp\SOUP\src\unused_private_fields.txt',
 'D:\CODE\Cshp\SOUP\src\unused_private_fields_all.txt',
 'D:\CODE\Cshp\SOUP\src\unused_private_fields_candidates.txt',
 'D:\CODE\Cshp\SOUP\src\unused_private_fields_report.txt',
 'D:\CODE\Cshp\SOUP\src\vetted_unused_private_fields_full.csv',
 'D:\CODE\Cshp\SOUP\src\vetted_unused_private_fields_top30.csv',
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

Write-Output "\nCleanup finished. Remaining paths status:"
foreach ($p in $paths) { Write-Output "$p -> " + (Test-Path $p) }
