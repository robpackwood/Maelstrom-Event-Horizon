[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$gameAssets = Join-Path $repoRoot 'MaelstromEventHorizon\Assets'
$packageImages = Join-Path $repoRoot 'MaelstromEventHorizon.Package\Images'
$outputPath = Join-Path $PSScriptRoot 'asset-manifest.csv'
$extensions = @('.ico', '.mp3', '.png', '.wav')

$sources = @{
    'MaelstromEventHorizon\Assets\through-the-universe.mp3' = @{
        Status = 'VERIFIED_EXACT'
        Work = 'Through The Universe'
        Author = 'Vitalezzz'
        License = 'CC0-1.0'
        Source = 'https://opengameart.org/content/through-the-universe'
        Note = 'Exact SHA-256 match to source MP3.'
    }
    'MaelstromEventHorizon\Assets\Music\singularity-action.mp3' = @{
        Status = 'VERIFIED_EXACT'
        Work = 'Singularity (Action)'
        Author = 'Vitalezzz'
        License = 'CC0-1.0'
        Source = 'https://opengameart.org/content/singularity-0'
        Note = 'Exact SHA-256 match to source MP3.'
    }
    'MaelstromEventHorizon\Assets\Music\wave-01-our-expanse.mp3' = @{
        Status = 'VERIFIED_EXACT'
        Work = 'Our Expanse (loop version)'
        Author = 'Bobjt'
        License = 'CC0-1.0'
        Source = 'https://opengameart.org/content/our-expanse'
        Note = 'Exact SHA-256 match to source MP3.'
    }
    'MaelstromEventHorizon\Assets\Music\wave-04-star-on-the-horizon.mp3' = @{
        Status = 'VERIFIED_EXACT'
        Work = 'Star On The Horizon'
        Author = 'Vitalezzz'
        License = 'CC0-1.0'
        Source = 'https://opengameart.org/content/star-on-the-horizon'
        Note = 'Exact SHA-256 match to source MP3.'
    }
    'MaelstromEventHorizon\Assets\Music\wave-07-magic-space.mp3' = @{
        Status = 'VERIFIED_EXACT'
        Work = 'Magic Space'
        Author = 'CodeManu'
        License = 'CC0-1.0'
        Source = 'https://opengameart.org/content/magic-space'
        Note = 'Exact SHA-256 match to source MP3.'
    }
    'MaelstromEventHorizon\Assets\Music\wave-09-anti-entity.mp3' = @{
        Status = 'VERIFIED_EXACT'
        Work = 'Anti Entity (loopable version)'
        Author = 'TAD'
        License = 'CC-BY-4.0'
        Source = 'https://opengameart.org/content/anti-entity'
        Note = 'Exact SHA-256 match to source MP3; renamed.'
    }
}

$outerSpaceTracks = @{
    'wave-02-lift-off.mp3' = 'Lift Off'
    'wave-05-racing-through-asteroids.mp3' = 'Racing Through Asteroids'
    'wave-06-emergency.mp3' = 'Emergency!'
    'wave-10-battle-in-outer-space.mp3' = 'Battle in Outer Space'
}

foreach ($entry in $outerSpaceTracks.GetEnumerator()) {
    $sources["MaelstromEventHorizon\Assets\Music\$($entry.Key)"] = @{
        Status = 'LICENSE_VERIFIED_TRANSCODE'
        Work = $entry.Value
        Author = 'Leonardo Paz'
        License = 'CC-BY-4.0'
        Source = 'https://opengameart.org/content/outer-space-music-pack'
        Note = 'Source page verified; OGG transcoded to MP3; conversion log unavailable.'
    }
}

$sources['MaelstromEventHorizon\Assets\Music\wave-08-the-calm-unknown.mp3'] = @{
    Status = 'LICENSE_VERIFIED_TRANSCODE'
    Work = 'The Calm Unknown'
    Author = 'Dizzy Crow / Daniel Michel'
    License = 'CC-BY-4.0'
    Source = 'https://opengameart.org/content/full-orchestral-soundtrack-8-tracks-1436'
    Note = 'Source page verified; OGG transcoded to MP3; conversion log unavailable.'
}

$newWaveTracks = @{
    'wave-11-outworld.mp3' = @('Outworld', 'Vitalezzz', 'CC0-1.0', 'https://opengameart.org/content/outworld', 'Exact SHA-256 match to source MP3.')
    'wave-12-gsf-discovery.mp3' = @('GSF Discovery', 'Vitalezzz', 'CC0-1.0', 'https://opengameart.org/content/gsf-discovery', 'Exact SHA-256 match to source MP3.')
    'wave-13-joining-forces.mp3' = @('Joining Forces', 'Vitalezzz', 'CC0-1.0', 'https://opengameart.org/content/joining-forces', 'Exact SHA-256 match to source MP3.')
    'wave-18-robotic-soundtrack.mp3' = @('Robotic', 'Fato Shadow', 'CC-BY-4.0', 'https://opengameart.org/content/robotic-soundtrack', 'Exact SHA-256 match to source MP3.')
    'wave-19-anti-entity-original.mp3' = @('Anti Entity', 'TAD', 'CC-BY-4.0', 'https://opengameart.org/content/anti-entity', 'Exact SHA-256 match to source MP3.')
    'wave-20-stillness-of-space.mp3' = @('Stillness of Space', 'Leonardo Paz', 'CC-BY-4.0', 'https://opengameart.org/content/outer-space-music-pack', 'Source OGG transcoded to MP3 with FFmpeg for game playback.')
}

foreach ($entry in $newWaveTracks.GetEnumerator()) {
    $track = $entry.Value
    $sources["MaelstromEventHorizon\Assets\Music\$($entry.Key)"] = @{
        Status = if ($entry.Key -eq 'wave-20-stillness-of-space.mp3') { 'LICENSE_VERIFIED_TRANSCODE' } else { 'VERIFIED_EXACT' }
        Work = $track[0]; Author = $track[1]; License = $track[2]; Source = $track[3]; Note = $track[4]
    }
}

$assetRoots = @($gameAssets)
if (Test-Path -LiteralPath $packageImages) {
    $assetRoots += $packageImages
}

$files = $assetRoots | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Recurse -File
} | Where-Object { $_.Extension.ToLowerInvariant() -in $extensions }

$rows = foreach ($file in $files | Sort-Object FullName) {
    $relativePath = $file.FullName.Substring($repoRoot.Length + 1)
    $source = $sources[$relativePath]

    if ($null -ne $source) {
        $status = $source.Status
        $work = $source.Work
        $author = $source.Author
        $license = $source.License
        $sourceUrl = $source.Source
        $note = $source.Note
    }
    elseif ($relativePath.StartsWith('MaelstromEventHorizon.Package\Images\')) {
        $status = 'PROJECT_GENERATED_DERIVATIVE_UNCLEARED_SOURCE'
        $work = ''
        $author = ''
        $license = ''
        $sourceUrl = 'MaelstromEventHorizon\Assets\maelstrom-icon.png'
        $note = 'Generated by Generate-StoreAssets.ps1; source icon requires clearance.'
    }
    else {
        $status = 'DECLARATION_REQUIRED'
        $work = ''
        $author = ''
        $license = ''
        $sourceUrl = ''
        $note = 'Hash and Git custody exist; ownership/license evidence was not found.'
    }

    [pscustomobject]@{
        RelativePath = $relativePath
        Category = if ($file.Extension -in @('.mp3', '.wav')) { 'Audio' } else { 'Image' }
        Bytes = $file.Length
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        ProvenanceStatus = $status
        Work = $work
        Author = $author
        License = $license
        Source = $sourceUrl
        Notes = $note
    }
}

$csv = $rows | ConvertTo-Csv -NoTypeInformation
$content = ($csv -join "`r`n") + "`r`n"
[System.IO.File]::WriteAllText($outputPath, $content, [System.Text.UTF8Encoding]::new($true))

Write-Output "Wrote $($rows.Count) asset records to $outputPath"
