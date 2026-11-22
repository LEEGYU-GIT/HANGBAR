param([int]$X = 300, [int]$Y = 300, [int]$W = 700, [int]$H = 400, [int]$ShowSeconds = 3, [int]$MoveBy = 250, [int]$AfterMoveSeconds = 3)
# Our own small top-most test window: appears, moves right, then minimizes and closes. Physical pixels.
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace Hb -Name Tw -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[Hb.Tw]::SetProcessDPIAware() | Out-Null
$f = New-Object System.Windows.Forms.Form
$f.Text = "HANGBAR terrain test"; $f.StartPosition = "Manual"; $f.TopMost = $true; $f.ShowInTaskbar = $true
$f.Bounds = New-Object System.Drawing.Rectangle $X, $Y, $W, $H
$step = 0
$t = New-Object System.Windows.Forms.Timer; $t.Interval = 1000
$t.Add_Tick({
  $script:step++
  if ($script:step -eq $ShowSeconds) { $f.Left = $f.Left + $MoveBy }
  if ($script:step -eq $ShowSeconds + $AfterMoveSeconds) { $f.WindowState = "Minimized" }
  if ($script:step -eq $ShowSeconds + $AfterMoveSeconds + 2) { $f.Close() }
})
$t.Start()
[System.Windows.Forms.Application]::Run($f)
