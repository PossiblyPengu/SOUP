$excelPath = "E:\CODE\important files\ALLO\flube.xlsx"

try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false

    $workbook = $excel.Workbooks.Open($excelPath)
    $worksheet = $workbook.Sheets.Item(1)

    Write-Host "=== First 10 rows of Excel file ===" -ForegroundColor Cyan
    Write-Host ""

    for ($row = 1; $row -le 10; $row++) {
        $rowData = @()
        for ($col = 1; $col -le 15; $col++) {
            $cell = $worksheet.Cells.Item($row, $col)
            $value = if ($cell.Text) { $cell.Text } else { "" }
            $rowData += $value
        }
        Write-Host "Row $row : $($rowData -join ' | ')"
    }

    $workbook.Close($false)
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
