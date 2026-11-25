param(
  [string]$Photo = "",
  [ValidateSet("", "beach", "pool", "house", "mountain")][string]$Scene = "",
  [int]$Seconds = 30,
  [ValidateSet("low", "medium")][string]$Quality = "medium",
  [string]$Out = ""
)
# Benchmark (G0 world, or a v2 scene with -Scene): 1920x1080 window, vsync off, fixed camera path. Prints the result JSON (and writes it to -Out).
$ErrorActionPreference = "Stop"
if ($Out -eq "") { $Out = Join-Path $env:TEMP "inpc-bench.json" }
if (Test-Path $Out) { Remove-Item $Out }
& (Join-Path $PSScriptRoot "run.ps1") -Photo $Photo -Scene $Scene -Quality $Quality `
  -GodotArgs @("--resolution", "1920x1080", "--position", "0,0") -Extra @("--bench=$Seconds", "--out=$Out") | Out-Null
if (-not (Test-Path $Out)) { throw "benchmark wrote no result" }
Get-Content $Out
