param([string]$Log = "loopback-bogus.log")
# ECC review reproduction: a bogus callback (any web page or local process can send one) must not end the sign-in.
# Runs the app in auth self-test mode, sends a fake "/callback?error=x" first, then the real-looking callback.
# Both requests must get an answer; before the fix the listener closed after the first one.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$job = Start-Job -ScriptBlock {
  Start-Sleep -Seconds 5
  $out = @()
  foreach ($q in @("error=x", "code=test")) {
    try { $code = (Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:53682/callback?$q" -TimeoutSec 8).StatusCode }
    catch { $code = "refused/timeout" }
    $out += "callback ?$q status=$code"
    Start-Sleep -Seconds 1
  }
  $out
}
$env:HANGBAR_AUTH_SELFTEST = "1"; $env:HANGBAR_CHAT = "0"
& "$here\run-app.ps1" -Seconds 16 -Log $Log | Out-Null
Remove-Item Env:HANGBAR_AUTH_SELFTEST, Env:HANGBAR_CHAT
Receive-Job $job -Wait
Get-Content $Log | Select-String "\[auth\]"
