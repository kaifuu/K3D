# Unity float32 semantics of Hash01(x,y,7): frac(sin(x*127.1+y*311.7+7*74.7)*43758.5453)
for ($gx=0; $gx -lt 4; $gx++) {
  $line = @()
  for ($gz=0; $gz -lt 4; $gz++) {
    $a = [float]([float]$gx * [float]127.1 + [float]$gz * [float]311.7 + [float]7 * [float]74.7)
    $s = [float][Math]::Sin($a)
    $h = [float]($s * [float]43758.5453)
    $frac = $h - [Math]::Floor($h)
    $line += ('g{0}_{1}={2:F3}{3}' -f $gx, $gz, $frac, $(if ($frac -lt 0.66) {'B'} elseif ($frac -lt 0.87) {'P'} else {'Z'}))
  }
  Write-Output ($line -join '  ')
}
