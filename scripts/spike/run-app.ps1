param(
  [int]$Seconds = 10,
  [string]$Shot = "",          # png path; captures only the bottom strip of the primary monitor
  [int]$StripHeight = 220,
  [string]$Log = "app.log",
  [string[]]$Extra = @()
)
$ErrorActionPreference = "Stop"
$godot = "C:\Users\ljsn1\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$app = "C:\Users\ljsn1\Desktop\Project\HANGBAR\client\app"
Remove-Item Env:ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $godot -ArgumentList (@("--path", $app) + $Extra) -RedirectStandardOutput $Log -RedirectStandardError "$Log.err" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds $Seconds
if ($Shot -ne "") {
  Add-Type -AssemblyName System.Windows.Forms, System.Drawing
  Add-Type -Namespace Hb -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
  [Hb.Dpi]::SetProcessDPIAware() | Out-Null   # physical pixels, same as the app
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $StripHeight
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.X, $b.Bottom - $StripHeight, 0, 0, $bmp.Size)
  $bmp.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
"exited=$($p.HasExited)"
