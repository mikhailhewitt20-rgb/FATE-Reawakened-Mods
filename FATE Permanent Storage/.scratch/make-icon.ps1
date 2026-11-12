Add-Type -AssemblyName System.Drawing
$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 26, 18, 11))
$g.FillRectangle($bg, 0, 0, $size, $size)
$border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 211, 170, 72), 6)
$g.DrawRectangle($border, 10, 10, 236, 236)
$wood = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 120, 72, 32))
$woodLight = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 158, 101, 48))
$dark = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 48, 28, 12), 6)
$gold = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 232, 190, 80))
$goldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 232, 190, 80), 5)
# body
$g.FillRectangle($wood, 48, 118, 160, 82)
$g.DrawRectangle($dark, 48, 118, 160, 82)
# lid
$g.FillRectangle($woodLight, 48, 88, 160, 38)
$g.DrawRectangle($dark, 48, 88, 160, 38)
$g.DrawArc($dark, 48, 58, 160, 62, 180, 180)
$g.FillPie($woodLight, 48, 58, 160, 62, 180, 180)
# bands
$g.DrawLine($goldPen, 82, 88, 82, 200)
$g.DrawLine($goldPen, 174, 88, 174, 200)
$g.DrawLine($goldPen, 48, 150, 208, 150)
# lock
$g.FillRectangle($gold, 112, 138, 32, 38)
$g.DrawRectangle($dark, 112, 138, 32, 38)
$keyhole = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 48, 28, 12))
$g.FillEllipse($keyhole, 121, 147, 14, 14)
$g.FillRectangle($keyhole, 125, 156, 6, 12)
# label
$font = New-Object System.Drawing.Font('Segoe UI', 15, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 222, 166))
$format = New-Object System.Drawing.StringFormat
$format.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('PERMANENT STORAGE', $font, $textBrush, (New-Object System.Drawing.RectangleF(0, 218, 256, 28)), $format)
$out = Join-Path $PSScriptRoot '..\distribution\fate-permanent-storage\icon.png'
$bmp.Save([IO.Path]::GetFullPath($out), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ([IO.Path]::GetFullPath($out))
