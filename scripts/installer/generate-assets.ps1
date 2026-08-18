# Gera o ícone e as imagens do assistente (gradiente índigo/ciano do Sentinela).
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$here = $PSScriptRoot

function Add-RoundedRect(
    [System.Drawing.Drawing2D.GraphicsPath]$path,
    [int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
    $d = [Math]::Max(2, $r * 2)
    if ($d -gt $w) { $d = $w }
    if ($d -gt $h) { $d = $h }
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
}

function New-SentinelaLogo([int]$size, [bool]$transparent) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    if ($transparent) {
        $g.Clear([System.Drawing.Color]::Transparent)
    } else {
        $g.Clear([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
    }

    $rect = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 99, 102, 241),
        [System.Drawing.Color]::FromArgb(255, 14, 165, 233),
        45.0)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $path 0 0 $size $size ([int][Math]::Round($size * 0.22))
    $g.FillPath($brush, $path)

    $penWidth = [Math]::Max(1.6, $size * 0.075)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White, $penWidth)
    $inset = $size * 0.18
    $g.DrawEllipse($pen, $inset, $inset, $size - (2 * $inset), $size - (2 * $inset))

    $inner = $size * 0.26
    $g.FillEllipse(
        [System.Drawing.Brushes]::White,
        ($size - $inner) / 2,
        ($size - $inner) / 2,
        $inner,
        $inner)

    $pen.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $g.Dispose()
    return $bmp
}

function Save-PngIcon([System.Drawing.Bitmap[]]$bitmaps, [string]$path) {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$bitmaps.Count)

    $payloads = @()
    foreach ($bmp in $bitmaps) {
        $img = New-Object System.IO.MemoryStream
        $bmp.Save($img, [System.Drawing.Imaging.ImageFormat]::Png)
        $payloads += , $img.ToArray()
        $img.Dispose()
    }

    $offset = 6 + (16 * $bitmaps.Count)
    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $bmp = $bitmaps[$i]
        $w = if ($bmp.Width -ge 256) { 0 } else { $bmp.Width }
        $h = if ($bmp.Height -ge 256) { 0 } else { $bmp.Height }
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $data = $payloads[$i]
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$offset)
        $offset += $data.Length
    }
    foreach ($data in $payloads) {
        $bw.Write($data)
    }
    $bw.Flush()
    [IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
}

function Save-WizardBmp([int]$width, [int]$height, [string]$path, [int]$logoSize) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $rect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 15, 23, 42),
        [System.Drawing.Color]::FromArgb(255, 30, 41, 79),
        90.0)
    $g.FillRectangle($brush, $rect)

    $logo = New-SentinelaLogo $logoSize $false
    $x = [int](($width - $logoSize) / 2)
    $y = [int](($height - $logoSize) / 3)
    $g.DrawImage($logo, $x, $y, $logoSize, $logoSize)

    $rect24 = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $bmp24 = $bmp.Clone($rect24, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $bmp24.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp24.Dispose()
    $logo.Dispose()
    $brush.Dispose()
    $g.Dispose()
    $bmp.Dispose()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$icons = foreach ($s in $sizes) { New-SentinelaLogo $s $true }
Save-PngIcon $icons (Join-Path $here "setup.ico")
foreach ($b in $icons) { $b.Dispose() }

Save-WizardBmp 164 314 (Join-Path $here "wizard-side.bmp") 88
Save-WizardBmp 55 55 (Join-Path $here "wizard-small.bmp") 47

Write-Host "Assets gerados em $here"
