param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePng,

    [Parameter(Mandatory = $true)]
    [string]$OutIco
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Image]::FromFile($SourcePng)
$bitmap = New-Object System.Drawing.Bitmap 256, 256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try {
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($source, 0, 0, 256, 256)

    $pngStream = New-Object System.IO.MemoryStream
    try {
        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
    }
    finally {
        $pngStream.Dispose()
    }

    $directory = Split-Path -Parent $OutIco
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $fileStream = [System.IO.File]::Create($OutIco)
    $writer = New-Object System.IO.BinaryWriter $fileStream
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]1)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$pngBytes.Length)
        $writer.Write([UInt32]22)
        $writer.Write($pngBytes)
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
    }
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
    $source.Dispose()
}
