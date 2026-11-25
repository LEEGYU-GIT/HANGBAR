param(
  [string]$Photo = "",
  [ValidateSet("", "beach", "pool", "house", "mountain")][string]$Scene = "",
  [Parameter(Mandatory = $true)][string]$Out
)
# Saves overview.png, ground.png, top.png and photo.png (the prepared photo) into -Out.
# Keep -Out outside the repository for your own photos; output names never include the photo's name.
$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "run.ps1") -Photo $Photo -Scene $Scene -GodotArgs @("--resolution", "1600x900") -Extra @("--shots=$Out") | Out-Null
Get-ChildItem $Out -Filter *.png | Select-Object Name, Length
