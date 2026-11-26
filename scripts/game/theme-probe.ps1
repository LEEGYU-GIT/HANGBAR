param(
  [Parameter(Mandatory = $true)][string]$Photos   # folder; subfolders named after scenes (beach, pool, house, mountain, ...) give accuracy
)
# Runs both theme model candidates (Places365, CLIP) over the photos and prints a numbered table and the accuracy.
# Photo names and paths are never printed. Models: see models/README.md.
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
& $dotnet run --project (Join-Path $root "tools\theme-probe") -v q -- "--photos=$Photos" "--models=$(Join-Path $root 'models')"
