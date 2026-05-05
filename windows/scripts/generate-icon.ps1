# Build a multi-resolution Windows .ico from the Mac source PNG.
# Run from repo root: .\windows\scripts\generate-icon.ps1
#
# Output: windows/WallP/Assets/WallP.ico
#
# Sizes: 16, 24, 32, 48, 64, 128, 256 (PNG-encoded entries - Vista+ supports this).

[CmdletBinding()]
param(
    [string]$SourcePng = (Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))) 'mac\WallP\Assets.xcassets\AppIcon.appiconset\AppIcon-1024.png'),
    [string]$OutputIco = (Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) 'WallP\Assets\WallP.ico')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SourcePng)) {
    Write-Error "Source PNG not found: $SourcePng"
    exit 1
}

$outDir = Split-Path -Parent $OutputIco
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)

# Resize source to each target size and capture as PNG bytes.
$source = [System.Drawing.Image]::FromFile($SourcePng)
$pngBlobs = @{}
try {
    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($source, 0, 0, $size, $size)
        } finally {
            $g.Dispose()
        }

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBlobs[$size] = $ms.ToArray()
        $ms.Dispose()
        $bmp.Dispose()
    }
} finally {
    $source.Dispose()
}

# Assemble the .ico container.
# https://en.wikipedia.org/wiki/ICO_(file_format)
#   ICONDIR (6 bytes):
#     reserved=0 (u16), type=1 (u16), count=N (u16)
#   ICONDIRENTRY (16 bytes per image):
#     width=size%256, height=size%256, colors=0, reserved=0,
#     planes=1, bpp=32, byteSize=u32, offset=u32

$entryCount = $sizes.Count
$headerSize = 6 + 16 * $entryCount

$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
try {
    # Header
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$entryCount)

    # Directory entries
    $offset = $headerSize
    foreach ($size in $sizes) {
        $blob = $pngBlobs[$size]
        $w = if ($size -ge 256) { [byte]0 } else { [byte]$size }
        $h = if ($size -ge 256) { [byte]0 } else { [byte]$size }
        $writer.Write($w)
        $writer.Write($h)
        $writer.Write([byte]0)        # color palette
        $writer.Write([byte]0)        # reserved
        $writer.Write([uint16]1)      # planes
        $writer.Write([uint16]32)     # bpp
        $writer.Write([uint32]$blob.Length)
        $writer.Write([uint32]$offset)
        $offset += $blob.Length
    }

    # Image data (PNGs concatenated)
    foreach ($size in $sizes) {
        $writer.Write($pngBlobs[$size])
    }

    [System.IO.File]::WriteAllBytes($OutputIco, $out.ToArray())
} finally {
    $writer.Dispose()
}

$icoSize = (Get-Item $OutputIco).Length
Write-Host ("Wrote {0} ({1:N0} bytes, {2} sizes: {3})" -f $OutputIco, $icoSize, $entryCount, ($sizes -join ', '))
