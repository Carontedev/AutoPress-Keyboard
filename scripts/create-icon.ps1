param([string]$Source = (Join-Path $PSScriptRoot '../src/AutoPress.App/Assets/logo.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $PSScriptRoot '../src/AutoPress.App/Assets'
[IO.Directory]::CreateDirectory($destination) | Out-Null
$original = Join-Path $destination 'logo.png'
if ([IO.Path]::GetFullPath($Source) -ne [IO.Path]::GetFullPath($original)) {
    Copy-Item -LiteralPath $Source -Destination $original
}
$bitmap = [Drawing.Bitmap]::new($original)
try {
    # Trim only fully transparent margins, retaining every visible pixel.
    $left = $bitmap.Width; $top = $bitmap.Height; $right = -1; $bottom = -1
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            if ($bitmap.GetPixel($x, $y).A -gt 0) {
                $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
                $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    if ($right -lt 0) { throw 'Logo is completely transparent.' }
    $crop = [Drawing.Rectangle]::new($left, $top, $right-$left+1, $bottom-$top+1)
    Write-Output "Visible logo bounds: $crop"
    $sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
    $frames = @()
    foreach ($size in $sizes) {
        $canvas = [Drawing.Bitmap]::new($size, $size)
        $graphics = [Drawing.Graphics]::FromImage($canvas)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scale = ($size * 0.94) / [Math]::Max($crop.Width, $crop.Height)
            $w = [single]($crop.Width * $scale); $h = [single]($crop.Height * $scale)
            $rect = [Drawing.RectangleF]::new(($size-$w)/2, ($size-$h)/2, $w, $h)
            $graphics.DrawImage($bitmap, $rect, $crop, [Drawing.GraphicsUnit]::Pixel)
            $canvas.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        } finally { $stream.Dispose(); $graphics.Dispose(); $canvas.Dispose() }
    }
    $file = [IO.File]::Create((Join-Path $destination 'autopress.ico'))
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = $sizes[$i] % 256
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $file.Dispose() }
} finally { $bitmap.Dispose() }
