# One-time generator for the Store package logos in Installer\Msix\Assets, built from Resources\AppIcon.ico.
# The PNGs are committed; re-run this only when the app icon changes:
#   powershell -ExecutionPolicy Bypass -File Installer\Msix\generate-assets.ps1
# Compatible with Windows PowerShell 5.1 (System.Drawing).

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$icoPath = Join-Path $repoRoot "Resources\AppIcon.ico"
$outDir = Join-Path $PSScriptRoot "Assets"

if (-not (Test-Path $icoPath)) {
    throw "Icon not found: $icoPath"
}

New-Item -ItemType Directory -Force $outDir | Out-Null

# Reads the ICONDIR/ICONDIRENTRY table so PNG-compressed frames (the 256 px one) can be decoded directly;
# System.Drawing.Icon cannot be relied on for those.
$icoBytes = [IO.File]::ReadAllBytes($icoPath)
$frameCount = [BitConverter]::ToUInt16($icoBytes, 4)
$pngFrames = @{}
for ($i = 0; $i -lt $frameCount; $i++) {
    $entry = 6 + 16 * $i
    $size = $icoBytes[$entry]
    if ($size -eq 0) { $size = 256 }
    $dataSize = [BitConverter]::ToUInt32($icoBytes, $entry + 8)
    $dataOffset = [BitConverter]::ToUInt32($icoBytes, $entry + 12)
    if ($icoBytes[$dataOffset] -eq 0x89 -and $icoBytes[$dataOffset + 1] -eq 0x50) {
        $pngFrames[$size] = [byte[]]($icoBytes[$dataOffset..($dataOffset + $dataSize - 1)])
    }
}

function Get-IcoFrame([int]$size) {
    if ($pngFrames.ContainsKey($size)) {
        $stream = New-Object System.IO.MemoryStream(, $pngFrames[$size])
        try {
            $image = [System.Drawing.Image]::FromStream($stream)
            # Copy so the bitmap doesn't depend on the stream.
            $copy = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [System.Drawing.Graphics]::FromImage($copy)
            try { $g.DrawImage($image, 0, 0, $size, $size) } finally { $g.Dispose(); $image.Dispose() }
            return $copy
        }
        finally {
            $stream.Dispose()
        }
    }

    $icon = New-Object System.Drawing.Icon($icoPath, $size, $size)
    try { return $icon.ToBitmap() } finally { $icon.Dispose() }
}

# Draws $source into a transparent square canvas of $canvas px, with the icon filling $content px (centred).
function New-ScaledBitmap([System.Drawing.Bitmap]$source, [int]$canvas, [int]$content) {
    $bitmap = New-Object System.Drawing.Bitmap($canvas, $canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $offset = [int](($canvas - $content) / 2)
        $g.DrawImage($source, $offset, $offset, $content, $content)
    }
    finally {
        $g.Dispose()
    }
    return $bitmap
}

function Save-Png([System.Drawing.Bitmap]$bitmap, [string]$name) {
    $path = Join-Path $outDir $name
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ("  {0} ({1}x{2})" -f $name, $bitmap.Width, $bitmap.Height)
}

$base = Get-IcoFrame 256
try {
    # A blank frame would produce blank logos; fail instead of committing them.
    $opaque = 0
    for ($y = 0; $y -lt 256; $y += 8) {
        for ($x = 0; $x -lt 256; $x += 8) {
            if ($base.GetPixel($x, $y).A -gt 0) { $opaque++ }
        }
    }
    if ($opaque -lt 50) {
        throw "The 256 px icon frame looks blank ($opaque sampled opaque pixels)."
    }

    Write-Host "Writing logos to $outDir"

    # Store logo and app list icon: icon full-bleed.
    foreach ($spec in @(@("StoreLogo.scale-100.png", 50), @("StoreLogo.scale-200.png", 100),
                        @("Square44x44Logo.scale-100.png", 44), @("Square44x44Logo.scale-200.png", 88))) {
        $bitmap = New-ScaledBitmap $base $spec[1] $spec[1]
        try { Save-Png $bitmap $spec[0] } finally { $bitmap.Dispose() }
    }

    # Taskbar / Start list sizes use the exact icon frames, plain and unplated (no tile background).
    foreach ($size in 16, 24, 32, 48, 256) {
        $frame = Get-IcoFrame $size
        try {
            Save-Png $frame "Square44x44Logo.targetsize-$size.png"
            Save-Png $frame "Square44x44Logo.targetsize-${size}_altform-unplated.png"
        }
        finally {
            $frame.Dispose()
        }
    }

    # Start tile: icon at ~66% of the tile on a transparent background.
    foreach ($spec in @(@("Square150x150Logo.scale-100.png", 150, 100), @("Square150x150Logo.scale-200.png", 300, 200))) {
        $bitmap = New-ScaledBitmap $base $spec[1] $spec[2]
        try { Save-Png $bitmap $spec[0] } finally { $bitmap.Dispose() }
    }
}
finally {
    $base.Dispose()
}

Write-Host "Done."
