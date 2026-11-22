param([int]$Seconds = 4)
# Shows our own borderless window covering the primary monitor (like a borderless-windowed game), then closes it.
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace Hb -Name Ff -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);'
[Hb.Ff]::SetProcessDPIAware() | Out-Null
$f = New-Object System.Windows.Forms.Form
$f.FormBorderStyle = "None"; $f.StartPosition = "Manual"
$f.Bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$f.BackColor = [System.Drawing.Color]::FromArgb(20, 40, 70); $f.TopMost = $true; $f.ShowInTaskbar = $false
$t = New-Object System.Windows.Forms.Timer; $t.Interval = $Seconds * 1000; $t.Add_Tick({ $f.Close() }); $t.Start()
$f.Add_Shown({ [Hb.Ff]::SetForegroundWindow($f.Handle) | Out-Null; $f.Activate() })
[System.Windows.Forms.Application]::Run($f)
