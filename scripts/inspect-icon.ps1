# Diagnostic only: render existing ICO frames over light and dark backgrounds.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$asset = Join-Path $root 'src/AutoPress.App/Assets'
$qa = Join-Path $root 'artifacts/qa'
[IO.Directory]::CreateDirectory($qa) | Out-Null
$source = [Drawing.Bitmap]::new((Join-Path $asset 'logo.png'))
try {
    $opaque = 0; $partial = 0; $transparent = 0
    for ($y=0; $y -lt $source.Height; $y++) {
        for ($x=0; $x -lt $source.Width; $x++) {
            $a = $source.GetPixel($x,$y).A
            if ($a -eq 255) { $opaque++ } elseif ($a -eq 0) { $transparent++ } else { $partial++ }
        }
    }
    Write-Output "Source $($source.Width)x$($source.Height): opaque=$opaque partial=$partial transparent=$transparent"
} finally { $source.Dispose() }
$reader = [IO.BinaryReader]::new([IO.File]::OpenRead((Join-Path $asset 'autopress.ico')))
$sheet = [Drawing.Bitmap]::new(840, 480)
$g = [Drawing.Graphics]::FromImage($sheet)
$font = [Drawing.Font]::new('Segoe UI',10)
try {
    $g.Clear([Drawing.Color]::FromArgb(245,245,245))
    [void]$reader.ReadUInt16(); [void]$reader.ReadUInt16(); $count=$reader.ReadUInt16()
    $entries=@()
    for($i=0;$i -lt $count;$i++) {
        $width=[int]$reader.ReadByte(); if($width -eq 0){$width=256}
        [void]$reader.ReadBytes(7); $length=$reader.ReadUInt32(); $offset=$reader.ReadUInt32()
        $entries+= [pscustomobject]@{Width=$width;Length=$length;Offset=$offset}
    }
    $column=0
    foreach($entry in $entries | Where-Object { $_.Width -in @(16,24,32,48) }) {
        $reader.BaseStream.Position=$entry.Offset
        $stream=[IO.MemoryStream]::new($reader.ReadBytes($entry.Length))
        $frame=[Drawing.Bitmap]::new($stream)
        try {
            $alpha=0.0; $opaque=0
            for($y=0;$y -lt $frame.Height;$y++){for($x=0;$x -lt $frame.Width;$x++){
                $a=$frame.GetPixel($x,$y).A; $alpha+=$a/255.0; if($a -eq 255){$opaque++}
            }}
            Write-Output "Frame $($entry.Width): coverage=$([Math]::Round(100*$alpha/($frame.Width*$frame.Height),1))% opaquePixels=$opaque"
            for($row=0;$row -lt 2;$row++){
                $tile=[Drawing.Bitmap]::new(200,210)
                $tg=[Drawing.Graphics]::FromImage($tile)
                try {
                    $bg=if($row -eq 0){[Drawing.Color]::FromArgb(58,54,64)}else{[Drawing.Color]::FromArgb(224,219,211)}
                    $tg.Clear($bg)
                    $tg.DrawImageUnscaled($frame, [int]((200-$frame.Width)/2), 10)
                    $tg.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                    $tg.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
                    $tg.DrawImage($frame,[Drawing.Rectangle]::new(36,70,128,128),0,0,$frame.Width,$frame.Height,[Drawing.GraphicsUnit]::Pixel)
                    $g.DrawImageUnscaled($tile,10+210*$column,40+220*$row)
                } finally {$tg.Dispose();$tile.Dispose()}
            }
            $g.DrawString("$($entry.Width) px / enlarged",$font,[Drawing.Brushes]::Black, [single](10+210*$column),[single]10)
        } finally {$frame.Dispose();$stream.Dispose()}
        $column++
    }
    $sheet.Save((Join-Path $qa 'icon-inspection.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {$font.Dispose();$g.Dispose();$sheet.Dispose();$reader.Dispose()}
