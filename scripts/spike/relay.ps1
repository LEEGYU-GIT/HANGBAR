param([ValidateSet("start","stop")][string]$Action, [string]$Log = "relay.log")
# Starts/stops the local wrangler dev relay on 127.0.0.1:8787, including its workerd children.
$server = "C:\Users\ljsn1\Desktop\Project\HANGBAR\server"
function Stop-Relay {
  Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like "*HANGBAR\server\node_modules\wrangler*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Get-Process workerd -ErrorAction SilentlyContinue | Stop-Process -Force
}
if ($Action -eq "stop") { Stop-Relay; "stopped"; return }
Stop-Relay
$env:WRANGLER_SEND_METRICS = "false"
Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "npx wrangler dev --ip 127.0.0.1 --port 8787 > `"$Log`" 2>&1" -WorkingDirectory $server -WindowStyle Hidden | Out-Null
for ($i = 0; $i -lt 60; $i++) { if ((Test-Path $Log) -and (Select-String -Path $Log -Pattern "Ready on" -Quiet)) { "ready"; return }; Start-Sleep -Milliseconds 500 }
"not ready"
