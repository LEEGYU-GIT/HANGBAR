param([int]$X = 500, [int]$Y = 500)
# Prints which top-level window would receive a click at (X,Y) in physical pixels (class name only, no titles).
Add-Type -Namespace Hb -Name Wfp -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern System.IntPtr WindowFromPoint(System.Drawing.Point p);
[DllImport("user32.dll")] public static extern System.IntPtr GetAncestor(System.IntPtr h, uint flags);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder sb, int n);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint pid);
'@ -ReferencedAssemblies System.Drawing
[Hb.Wfp]::SetProcessDPIAware() | Out-Null
$h = [Hb.Wfp]::WindowFromPoint((New-Object System.Drawing.Point $X, $Y))
$root = [Hb.Wfp]::GetAncestor($h, 2)
$sb = New-Object System.Text.StringBuilder 256
[Hb.Wfp]::GetClassName($root, $sb, 256) | Out-Null
$pid2 = 0; [Hb.Wfp]::GetWindowThreadProcessId($root, [ref]$pid2) | Out-Null
$proc = (Get-Process -Id $pid2 -ErrorAction SilentlyContinue).ProcessName
"at=($X,$Y) hwnd=$([int64]$root) class=$($sb.ToString()) process=$proc"
