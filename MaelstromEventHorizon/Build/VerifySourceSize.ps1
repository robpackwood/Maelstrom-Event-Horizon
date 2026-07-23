param(
    [Parameter(Mandatory = $true)]
    [string] $SourceRoot,
    [int] $MaximumLines = 500
)

$violations = Get-ChildItem -LiteralPath $SourceRoot -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $lineCount = [System.IO.File]::ReadLines($_.FullName).Count
        if ($lineCount -gt $MaximumLines) {
            [pscustomobject]@{ Path = $_.FullName; Lines = $lineCount }
        }
    }

if ($violations) {
    $violations | ForEach-Object {
        Write-Error "$($_.Path) contains $($_.Lines) lines; maximum is $MaximumLines."
    }
    exit 1
}
