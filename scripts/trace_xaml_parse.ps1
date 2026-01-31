param(
    [string]$Path = "d:\CODE\Cshp\SOUP\src\Views\ExpireWise\ExpireWiseView.xaml"
)
$settings = New-Object System.Xml.XmlReaderSettings
$settings.DtdProcessing = 'Prohibit'
$settings.IgnoreComments = $true
$settings.IgnoreProcessingInstructions = $true
$settings.IgnoreWhitespace = $true

$stack = New-Object System.Collections.Generic.Stack[string]

try {
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    while ($reader.Read()) {
        switch ($reader.NodeType) {
            'Element' {
                    if (-not $reader.IsEmptyElement) { $stack.Push([pscustomobject]@{Name=$reader.Name; Line=$reader.LineNumber}) }
            }
            'EndElement' {
                if ($stack.Count -eq 0) { Write-Host "Unmatched end element: $($reader.Name) at Line $($reader.LineNumber)"; break }
                    $top = $stack.Pop()
                    if ($top.Name -ne $reader.Name) {
                        Write-Host "Mismatch: start <$($top.Name)> (line $($top.Line)) vs end </$($reader.Name)> at Line $($reader.LineNumber)"
                        break
                    }
            }
        }
    }
    if ($stack.Count -gt 0) {
        Write-Host "Unclosed elements remain on stack (top first):"
            $stack | ForEach-Object { Write-Host "<$($_.Name)> opened at line $($_.Line)" }
    } else {
        Write-Host "Parsed OK"
    }
} catch {
    Write-Host "Parse exception: $($_.Exception.Message)"
    if ($stack.Count -gt 0) { Write-Host "Stack snapshot (top first):"; $stack | ForEach-Object { Write-Host $_ } }
}
