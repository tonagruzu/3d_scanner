$ErrorActionPreference = 'Stop'
$path = "d:\REPOS\3D_SCANNER\docs\Mata.jpeg"
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($path)
$w = $bmp.Width
$h = $bmp.Height

function Get-GrayRow([System.Drawing.Bitmap]$img, [int]$row) {
    $arr = New-Object 'double[]' $img.Width
    for ($x = 0; $x -lt $img.Width; $x++) {
        $p = $img.GetPixel($x, $row)
        $arr[$x] = 0.299 * $p.R + 0.587 * $p.G + 0.114 * $p.B
    }
    return $arr
}

function Get-GrayCol([System.Drawing.Bitmap]$img, [int]$col) {
    $arr = New-Object 'double[]' $img.Height
    for ($y = 0; $y -lt $img.Height; $y++) {
        $p = $img.GetPixel($col, $y)
        $arr[$y] = 0.299 * $p.R + 0.587 * $p.G + 0.114 * $p.B
    }
    return $arr
}

function Get-LineMetrics([double[]]$g) {
    $n = $g.Length
    $d = New-Object 'double[]' ($n - 1)
    for ($i = 0; $i -lt $n - 1; $i++) {
        $d[$i] = [Math]::Abs($g[$i + 1] - $g[$i])
    }

    $m = ($d | Measure-Object -Average).Average
    $s = [Math]::Sqrt((($d | ForEach-Object { ($_ - $m) * ($_ - $m) } | Measure-Object -Sum).Sum) / ($n - 1))
    $thr = $m + 2.0 * $s

    $edges = @()
    for ($i = 1; $i -lt $n - 2; $i++) {
        if ($d[$i] -gt $thr -and $d[$i] -ge $d[$i - 1] -and $d[$i] -ge $d[$i + 1]) {
            $edges += $i
        }
    }

    $sp = @()
    for ($i = 1; $i -lt $edges.Count; $i++) {
        $delta = $edges[$i] - $edges[$i - 1]
        if ($delta -ge 8 -and $delta -le 120) {
            $sp += $delta
        }
    }

    if ($sp.Count -lt 5) {
        return [pscustomobject]@{ Count = $sp.Count; Mean = 0; Std = 0; Mode = 0 }
    }

    $sorted = $sp | Sort-Object
    $lo = [int][Math]::Floor($sorted.Count * 0.1)
    $hi = [int][Math]::Ceiling($sorted.Count * 0.9)
    if ($hi -le $lo) {
        $lo = 0
        $hi = $sorted.Count
    }

    $trim = $sorted[$lo..($hi - 1)]
    $tm = ($trim | Measure-Object -Average).Average
    $ts = [Math]::Sqrt((($trim | ForEach-Object { ($_ - $tm) * ($_ - $tm) } | Measure-Object -Sum).Sum) / $trim.Count)

    $bins = @{}
    foreach ($v in $trim) {
        $b = [int]([Math]::Round($v / 2.0) * 2)
        if (-not $bins.ContainsKey($b)) { $bins[$b] = 0 }
        $bins[$b]++
    }

    $mode = ($bins.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
    return [pscustomobject]@{
        Count = $trim.Count
        Mean = [Math]::Round($tm, 2)
        Std = [Math]::Round($ts, 2)
        Mode = $mode
    }
}

$rowIdxs = @([int]($h * 0.35), [int]($h * 0.5), [int]($h * 0.65))
$colIdxs = @([int]($w * 0.35), [int]($w * 0.5), [int]($w * 0.65))

$rowRes = foreach ($r in $rowIdxs) {
    Get-LineMetrics (Get-GrayRow $bmp $r)
}

$colRes = foreach ($c in $colIdxs) {
    Get-LineMetrics (Get-GrayCol $bmp $c)
}

$bmp.Dispose()

Write-Output "ImageSize: ${w}x${h}"
Write-Output "Rows:"
$rowRes | ForEach-Object { Write-Output ("  Count={0} Mean={1} Std={2} Mode={3}" -f $_.Count, $_.Mean, $_.Std, $_.Mode) }
Write-Output "Cols:"
$colRes | ForEach-Object { Write-Output ("  Count={0} Mean={1} Std={2} Mode={3}" -f $_.Count, $_.Mean, $_.Std, $_.Mode) }
