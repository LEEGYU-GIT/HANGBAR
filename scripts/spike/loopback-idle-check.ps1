param([string]$Log = "loopback-idle.log")
# Review I8 reproduction: an idle connection to the sign-in port must not block the real callback.
# Runs the app in auth self-test mode, opens an idle TCP connection first, then sends the callback.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$job = Start-Job -ScriptBlock {
  Start-Sleep -Seconds 5
  $idle = [System.Net.Sockets.TcpClient]::new("127.0.0.1", 53682)   # connects, never sends
  Start-Sleep -Seconds 1
  $sw = [Diagnostics.Stopwatch]::StartNew()
  try { $code = (Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:53682/callback?code=test" -TimeoutSec 8).StatusCode }
  catch { $code = "timeout/error" }
  "callback status=$code after $([int]$sw.Elapsed.TotalMilliseconds)ms"
  $idle.Close()
}
$env:HANGBAR_AUTH_SELFTEST = "1"; $env:HANGBAR_CHAT = "0"
& "$here\run-app.ps1" -Seconds 16 -Log $Log | Out-Null
Remove-Item Env:HANGBAR_AUTH_SELFTEST, Env:HANGBAR_CHAT
Receive-Job $job -Wait
Get-Content $Log | Select-String "\[auth\]"
