param([string]$Path = 'd:\CODE\Cshp\SOUP\src\Views\ExpireWise\ExpireWiseView.xaml')
$text = Get-Content $Path -Raw
Function ListTags($name) {
    $openPat = "<$name[\s>]"
    $closePat = "</$name>"
    $openMatches = [regex]::Matches($text, $openPat, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $closeMatches = [regex]::Matches($text, $closePat, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    Write-Host "--- $name openings ($($openMatches.Count)) ---"
    for ($i=0; $i -lt $openMatches.Count; $i++) {
        $pos = $openMatches[$i].Index
        $line = ($text.Substring(0,$pos) -split "\r?\n").Count
        Write-Host ($i+1) "open at line" $line
    }
    Write-Host "--- $name closings ($($closeMatches.Count)) ---"
    for ($i=0; $i -lt $closeMatches.Count; $i++) {
        $pos = $closeMatches[$i].Index
        $line = ($text.Substring(0,$pos) -split "\r?\n").Count
        Write-Host ($i+1) "close at line" $line
    }
}
ListTags 'Grid'
ListTags 'Border'
