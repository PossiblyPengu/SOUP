$lite = 'C:\Users\acalabrese\.nuget\packages\litedb\5.0.21\lib\netstandard2.0\LiteDB.dll'
if (-not (Test-Path $lite)) { Write-Error "LiteDB.dll not found at $lite"; exit 2 }
Add-Type -Path $lite
$dbPath = Join-Path $env:APPDATA 'SOUP\OrderLog\orders.db'
if (-not (Test-Path $dbPath)) { Write-Output "DB not found: $dbPath"; exit 3 }
$db = [LiteDB.LiteDatabase]::new($dbPath)
$col = $db.GetCollection([LiteDB.BsonDocument], 'orders')
$all = $col.FindAll() | ForEach-Object { $_ }
Write-Output "Total orders in DB: $($all.Count)"

function IsBlank($doc) {
    try {
        $v = $doc["VendorName"]
        $t = $doc["TransferNumbers"]
        $w = $doc["WhsShipmentNumbers"]
        $n = $doc["NoteContent"]
        $vStr = if ($v -ne $null) { $v.AsString } else { '' }
        $tStr = if ($t -ne $null) { $t.AsString } else { '' }
        $wStr = if ($w -ne $null) { $w.AsString } else { '' }
        $nStr = if ($n -ne $null) { $n.AsString } else { '' }
        return ([string]::IsNullOrWhiteSpace($vStr) -and [string]::IsNullOrWhiteSpace($tStr) -and [string]::IsNullOrWhiteSpace($wStr) -and [string]::IsNullOrWhiteSpace($nStr))
    } catch { return $false }
}

$blanks = @()
foreach ($doc in $all) {
    if (IsBlank $doc) { $blanks += $doc }
}
Write-Output "Blank (practically-empty) orders: $($blanks.Count)"
if ($blanks.Count -gt 0) {
    Write-Output "--- Blank items ---"
    foreach ($b in $blanks) {
        $id = if ($b.ContainsKey('_id')) { $b['_id'] } else { $b['Id'] }
        $created = if ($b.ContainsKey('CreatedAt')) { $b['CreatedAt'] } else { '' }
        Write-Output ("Id: {0} CreatedAt: {1}" -f $id, $created)
    }
}

# Also print first few records with empty VendorName but non-empty other fields
$emptyVendor = $all | Where-Object {
    try { $v = $_['VendorName']; $v -eq $null -or [string]::IsNullOrWhiteSpace($v.AsString) } catch { $false }
}
Write-Output "Records with empty VendorName: $($emptyVendor.Count)"

$db.Dispose()
exit 0
