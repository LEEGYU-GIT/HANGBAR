param([switch]$Shots)
# Opens the scenic test (columnar basalt + waterfall) with Godot 4.7.2 .NET from winget.
# Drag = look around, wheel = zoom, 1/2/3 = quality, Space = toggle the slow drift.
# -Shots renders four views into .\shots and quits.
$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4.7.2-stable_mono_win64.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $godot) { throw "Godot 4.7.2 .NET (winget GodotEngine.GodotEngine.Mono) not found" }
$args = @("--path", $PSScriptRoot)
if ($Shots) { $args += @("--", "--shots=$PSScriptRoot\shots") }
& $godot.FullName @args
