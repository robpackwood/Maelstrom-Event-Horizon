[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $projectRoot 'MaelstromEventHorizon\Assets\maelstrom-icon.png'
$outputDirectory = Join-Path $PSScriptRoot 'Images'

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Source icon not found: $sourcePath"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

function New-Canvas {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [int]$Width,

        [Parameter(Mandatory)]
        [int]$Height,

        [Parameter(Mandatory)]
        [double]$IconScale,

        [Parameter(Mandatory)]
        [System.Drawing.Image]$Source,

        [switch]$Transparent
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        if ($Transparent) {
            $graphics.Clear([System.Drawing.Color]::Transparent)
        }
        else {
            $backgroundRectangle = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
            $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
                $backgroundRectangle,
                [System.Drawing.Color]::FromArgb(255, 3, 8, 16),
                [System.Drawing.Color]::FromArgb(255, 7, 24, 38),
                25.0)

            try {
                $graphics.FillRectangle($background, $backgroundRectangle)
            }
            finally {
                $background.Dispose()
            }

            $random = [System.Random]::new(($Width * 397) + $Height)
            $starCount = [Math]::Max(8, [int](($Width * $Height) / 14000))

            for ($index = 0; $index -lt $starCount; $index++) {
                $alpha = $random.Next(45, 155)
                $size = [Math]::Max(1, [int]($Width / 420))
                $x = $random.Next(0, $Width)
                $y = $random.Next(0, $Height)
                $starBrush = [System.Drawing.SolidBrush]::new(
                    [System.Drawing.Color]::FromArgb($alpha, 184, 224, 255))

                try {
                    $graphics.FillEllipse($starBrush, $x, $y, $size, $size)
                }
                finally {
                    $starBrush.Dispose()
                }
            }
        }

        $iconSize = [int]([Math]::Min($Width, $Height) * $IconScale)
        $iconX = [int](($Width - $iconSize) / 2)
        $iconY = [int](($Height - $iconSize) / 2)
        $destination = [System.Drawing.Rectangle]::new($iconX, $iconY, $iconSize, $iconSize)
        $graphics.DrawImage($Source, $destination)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$source = [System.Drawing.Image]::FromFile($sourcePath)

try {
    New-Canvas -Path (Join-Path $outputDirectory 'Square150x150Logo.scale-200.png') `
        -Width 300 -Height 300 -IconScale 0.9 -Source $source
    New-Canvas -Path (Join-Path $outputDirectory 'Square44x44Logo.scale-200.png') `
        -Width 88 -Height 88 -IconScale 0.92 -Source $source
    New-Canvas -Path (Join-Path $outputDirectory 'Square44x44Logo.targetsize-24_altform-unplated.png') `
        -Width 24 -Height 24 -IconScale 1.0 -Source $source -Transparent
    New-Canvas -Path (Join-Path $outputDirectory 'StoreLogo.png') `
        -Width 50 -Height 50 -IconScale 0.92 -Source $source
    New-Canvas -Path (Join-Path $outputDirectory 'Wide310x150Logo.scale-200.png') `
        -Width 620 -Height 300 -IconScale 0.9 -Source $source
    New-Canvas -Path (Join-Path $outputDirectory 'SplashScreen.scale-200.png') `
        -Width 1240 -Height 600 -IconScale 0.88 -Source $source
}
finally {
    $source.Dispose()
}

Write-Output "Generated Store assets in $outputDirectory"
