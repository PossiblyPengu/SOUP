$path='d:\CODE\Cshp\SOUP\src\Views\ExpireWise\ExpireWiseView.xaml'
$t = Get-Content $path -Raw
Function CountMatches($text,$pattern){ return [regex]::Matches($text,$pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline).Count }
Write-Host "<Grid: " (CountMatches $t '<Grid[\s>]')
Write-Host "</Grid>: " (CountMatches $t '</Grid>')
Write-Host "<Border: " (CountMatches $t '<Border[\s>]')
Write-Host "</Border>: " (CountMatches $t '</Border>')
Write-Host "<UserControl: " (CountMatches $t '<UserControl[\s>]')
Write-Host "</UserControl>: " (CountMatches $t '</UserControl>')
