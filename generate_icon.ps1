# Regenerates Assets/app.png and Assets/app.ico (flat "stacked notes" icon).
# Run from the repo root:  powershell -ExecutionPolicy Bypass -File generate_icon.ps1
Add-Type -AssemblyName System.Drawing

$tile  = [System.Drawing.ColorTranslator]::FromHtml('#74C7B8')   # muted pastel teal
$glyph = [System.Drawing.ColorTranslator]::FromHtml('#12142B')   # dark navy

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Design grid is 64x64: colour tile, back note (faded), front note.
function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 64.0, $size / 64.0)

    $b = New-Object System.Drawing.SolidBrush($tile)
    $g.FillPath($b, (New-RoundRect 0 0 64 64 15)); $b.Dispose()
    $b = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(115, $glyph))
    $g.FillPath($b, (New-RoundRect 22 9 33 33 5)); $b.Dispose()
    $b = New-Object System.Drawing.SolidBrush($glyph)
    $g.FillPath($b, (New-RoundRect 9 22 33 33 5)); $b.Dispose()
    $g.Dispose()
    return $bmp
}

$assets = Join-Path $PSScriptRoot 'Assets'

$png = New-IconBitmap 1024
$png.Save((Join-Path $assets 'app.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()

# ICO: classic 32-bit bitmap frames for small sizes (widest compatibility), PNG frame for 256.
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    if ($s -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        $w = New-Object System.IO.BinaryWriter($ms)
        $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
        $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
            }
        }
        $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
        $w.Write((New-Object byte[] ($maskRow * $s)))
        $w.Flush()
    }
    $bmp.Dispose()
    , $ms.ToArray()
}

$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($ico)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$frames[$i].Length); $w.Write([int]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'app.ico'), $ico.ToArray())
"Wrote app.png and app.ico ($($sizes -join ', ') px)"
