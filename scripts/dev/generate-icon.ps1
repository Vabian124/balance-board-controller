# Generates balance-board-icon.ico (32x32) for ApplicationIcon / taskbar.
$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$iconPath = Join-Path $root "src\BalanceBoard.App\Assets\balance-board-icon.ico"

Add-Type -AssemblyName System.Drawing

function New-BoardBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))

    $s = $size / 64.0
    $board = New-Object System.Drawing.RectangleF (4 * $s), (10 * $s), (56 * $s), (44 * $s)
    $g.FillRectangle(
        (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(248, 250, 252))),
        $board)
    $borderColor = [System.Drawing.Color]::FromArgb(148, 163, 184)
    $penWidth = [Math]::Max(1.0, 2 * $s)
    $pen = New-Object System.Drawing.Pen($borderColor, $penWidth)
    $g.DrawRectangle($pen, $board.X, $board.Y, $board.Width, $board.Height)

    $r = 7 * $s
    $dots = @(
        @(16, 22, [System.Drawing.Color]::FromArgb(74, 144, 217)),
        @(48, 22, [System.Drawing.Color]::FromArgb(245, 197, 66)),
        @(16, 42, [System.Drawing.Color]::FromArgb(92, 184, 92)),
        @(48, 42, [System.Drawing.Color]::FromArgb(232, 93, 93))
    )
    foreach ($d in $dots) {
        $brush = New-Object System.Drawing.SolidBrush $d[2]
        $g.FillEllipse($brush, ($d[0] * $s - $r), ($d[1] * $s - $r), ($r * 2), ($r * 2))
    }

    $cr = 5 * $s
    $g.FillEllipse(
        (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(225, 29, 72))),
        (32 * $s - $cr), (32 * $s - $cr), ($cr * 2), ($cr * 2))
    $strokeWidth = [Math]::Max(1.0, 2 * $s)
    $whitePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $strokeWidth
    $g.DrawEllipse($whitePen,
        (32 * $s - $cr), (32 * $s - $cr), ($cr * 2), ($cr * 2))

    $g.Dispose()
    return $bmp
}

$bmp = New-BoardBitmap 32
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::Create($iconPath)
try {
    $icon.Save($fs)
}
finally {
    $fs.Close()
    $icon.Dispose()
    $bmp.Dispose()
}

Write-Host "Wrote $iconPath ($((Get-Item $iconPath).Length) bytes)"
