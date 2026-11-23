param([int]$Seconds = 180, [int]$Warmup = 20, [string]$Out = "measure.csv")
# Measures the exported release build from outside: average CPU % (all cores) and working set, per state.
$exe = (Join-Path $PSScriptRoot "..\..\client\app\export\Hangbar.exe")
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$states = @(
  @{ Name = "normal_1_figure";   Env = @{ HANGBAR_CHAT = "0" } },
  @{ Name = "normal_3_figures";  Env = @{ HANGBAR_CHAT = "0"; HANGBAR_FIGURES = "3" } },
  @{ Name = "terrain_poll_off";  Env = @{ HANGBAR_CHAT = "0"; HANGBAR_TERRAIN_POLL = "0" } },
  @{ Name = "hidden_fullscreen"; Env = @{ HANGBAR_CHAT = "0" }; Hide = $true }
)
"state,seconds,cpu_avg_pct,ws_avg_mb,ws_max_mb" | Out-File $Out -Encoding utf8
foreach ($s in $states) {
  foreach ($k in $s.Env.Keys) { Set-Item "Env:$k" $s.Env[$k] }
  $p = Start-Process $exe -PassThru
  Start-Sleep -Seconds $Warmup
  $cover = $null
  if ($s.Hide) { $cover = Start-Process powershell -ArgumentList "-NoProfile", "-File", "`"$here\fake-fullscreen.ps1`"", "-Seconds", ($Seconds + 15) -PassThru -WindowStyle Hidden; Start-Sleep -Seconds 5 }
  $p.Refresh(); $cpu0 = $p.TotalProcessorTime; $t0 = Get-Date
  $ws = @()
  for ($i = 0; $i -lt $Seconds; $i += 10) { Start-Sleep -Seconds 10; $p.Refresh(); $ws += $p.WorkingSet64 / 1MB }
  $p.Refresh(); $cpu = ($p.TotalProcessorTime - $cpu0).TotalMilliseconds / ((Get-Date) - $t0).TotalMilliseconds / [Environment]::ProcessorCount * 100
  "{0},{1},{2:0.00},{3:0},{4:0}" -f $s.Name, $Seconds, $cpu, ($ws | Measure-Object -Average).Average, ($ws | Measure-Object -Maximum).Maximum | Out-File $Out -Append -Encoding utf8
  Stop-Process -Id $p.Id -Force
  if ($cover) { Stop-Process -Id $cover.Id -Force -ErrorAction SilentlyContinue }
  foreach ($k in $s.Env.Keys) { Remove-Item "Env:$k" -ErrorAction SilentlyContinue }
  Start-Sleep -Seconds 3
}
Get-Content $Out
