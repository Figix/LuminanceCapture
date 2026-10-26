# Generates src/LuminanceCapture.App/app.ico: a disc whose left half is color bands and right half the matching Rec.709 grays.
# Usage: pwsh tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$out = Join-Path $PSScriptRoot '..\src\LuminanceCapture.App\app.ico'

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $pad = [Math]::Max(1, [int]($s * 0.06))
    $d = $s - 2 * $pad
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($pad, $pad, $d, $d)
    $g.SetClip($path)
    $bands = @(
        @{ c = [System.Drawing.Color]::FromArgb(230, 57, 53);  y = [System.Drawing.Color]::FromArgb(64, 64, 64) },
        @{ c = [System.Drawing.Color]::FromArgb(67, 160, 71);  y = [System.Drawing.Color]::FromArgb(150, 150, 150) },
        @{ c = [System.Drawing.Color]::FromArgb(30, 100, 220); y = [System.Drawing.Color]::FromArgb(40, 40, 40) }
    )
    $bh = $d / 3.0
    for ($i = 0; $i -lt 3; $i++) {
        $top = $pad + $i * $bh
        $b1 = New-Object System.Drawing.SolidBrush $bands[$i].c
        $b2 = New-Object System.Drawing.SolidBrush $bands[$i].y
        $g.FillRectangle($b1, $pad, $top, $d / 2.0, $bh + 1)
        $g.FillRectangle($b2, $pad + $d / 2.0, $top, $d / 2.0, $bh + 1)
        $b1.Dispose(); $b2.Dispose()
    }
    $g.ResetClip()
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 70, 70)), ([Math]::Max(1.0, $s / 32.0))
    $g.DrawEllipse($pen, $pad, $pad, $d, $d)
    $pen.Dispose(); $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$pngs = foreach ($s in $sizes) { , (New-IconPng $s) }
$fs = [System.IO.File]::Create((Resolve-Path (Split-Path $out)).Path + '\app.ico')
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Dispose(); $fs.Dispose()
Write-Host "wrote $out"
