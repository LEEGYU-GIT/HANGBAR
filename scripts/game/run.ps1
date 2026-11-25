param(
  [string]$Photo = "",
  [ValidateSet("", "beach", "pool", "house", "mountain")][string]$Scene = "",   # v2 scene; empty = the G0 world
  [ValidateSet("low", "medium")][string]$Quality = "medium",
  [string[]]$Extra = @(),          # more user args, e.g. "--bench=30"
  [string[]]$GodotArgs = @()       # engine args before "--", e.g. "--resolution","1920x1080"
)
# Builds the INPC game project and runs it. The photo path goes to the game only; nothing here logs it.
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$game = Join-Path $root "game"
$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4.7.2-stable_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $godot) { throw "Godot 4.7.2 .NET (winget GodotEngine.GodotEngine.Mono) not found" }

& $dotnet build (Join-Path $game "Inpc.Game.csproj") --nologo -v q | Out-Host
if ($LASTEXITCODE -ne 0) { throw "build failed" }
if (-not (Test-Path (Join-Path $game ".godot"))) {
  & $godot.FullName --headless --path $game --import | Out-Null   # first run: let Godot create its project cache
}

$user = @("--quality=$Quality") + $Extra
if ($Scene -ne "") { $user = @("--scene=$Scene") + $user }
if ($Photo -ne "") { $user = @("--photo=$Photo") + $user }
& $godot.FullName (@("--path", $game) + $GodotArgs + @("--") + $user)
