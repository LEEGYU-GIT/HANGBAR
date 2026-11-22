param([int]$Y = 970, [int]$FromX = 0, [int]$ToX = 1919, [int]$Step = 8, [int]$DelayMs = 15)
# Moves the cursor only (no clicks) across a horizontal line in physical pixels, then restores it.
Add-Type -Namespace Hb -Name Cur -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern bool GetCursorPos(out System.Drawing.Point p);
'@ -ReferencedAssemblies System.Drawing
[Hb.Cur]::SetProcessDPIAware() | Out-Null
$orig = New-Object System.Drawing.Point
[Hb.Cur]::GetCursorPos([ref]$orig) | Out-Null
for ($x = $FromX; $x -le $ToX; $x += $Step) { [Hb.Cur]::SetCursorPos($x, $Y) | Out-Null; Start-Sleep -Milliseconds $DelayMs }
[Hb.Cur]::SetCursorPos($orig.X, $orig.Y) | Out-Null
