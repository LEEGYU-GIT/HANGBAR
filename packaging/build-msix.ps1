param([string]$Version = "0.0.1.0")
# Exports the Godot app, packs it as MSIX and signs it with a self-signed TEST certificate (CN=HangbarSpike).
# Output: packaging/out/Hangbar_<Version>_x64.msix. The .pfx stays in packaging/out (git-ignored).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root "client\app"
$pkg = $PSScriptRoot
$out = Join-Path $pkg "out"
$stage = Join-Path $out "stage"
$godot = "C:\Users\ljsn1\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$sdk = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
$env:PATH = "C:\Program Files\dotnet;" + $env:PATH

# 1. Godot export
$exportDir = Join-Path $app "export"
New-Item -ItemType Directory -Force $exportDir | Out-Null
New-Item -ItemType File -Force (Join-Path $exportDir ".gdignore") | Out-Null
Remove-Item (Join-Path $exportDir "Hangbar.exe") -ErrorAction SilentlyContinue
# Godot writes progress to stderr; run through cmd so PowerShell 5.1 does not turn it into a terminating error.
cmd /c "`"$godot`" --headless --path `"$app`" --export-release `"Windows Desktop`" `"$(Join-Path $exportDir 'Hangbar.exe')`" > `"$(Join-Path $out 'godot-export.log')`" 2>&1"
if (-not (Test-Path (Join-Path $exportDir "Hangbar.exe"))) { throw "Godot export failed (see out/godot-export.log)" }

# 1b. Per-monitor DPI awareness v2 (review C1). Godot itself asks only for system awareness; a manifest wins.
#     The pck is exported as a separate file (embed_pck=false) so rewriting exe resources cannot damage it.
$exe = Join-Path $exportDir "Hangbar.exe"
$mt = Join-Path $sdk "mt.exe"
& $mt -nologo -inputresource:"$exe;#1" -manifest (Join-Path $pkg "dpi.manifest") -outputresource:"$exe;#1" *> (Join-Path $out "mt.log")
if ($LASTEXITCODE -ne 0) {
  & $mt -nologo -manifest (Join-Path $pkg "dpi.manifest") -outputresource:"$exe;#1" *>> (Join-Path $out "mt.log")
  if ($LASTEXITCODE -ne 0) { throw "mt.exe failed (see out/mt.log)" }
}

# 2. Stage: exported files + manifest (with version) + assets
Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Get-ChildItem $exportDir -Exclude ".gdignore" | Copy-Item -Destination $stage -Recurse
Copy-Item (Join-Path $pkg "Assets") -Destination (Join-Path $stage "Assets") -Recurse
# -creplace: case-sensitive, so the XML declaration's version="1.0" is left alone; only <Identity Version=...> changes.
$manifest = (Get-Content (Join-Path $pkg "AppxManifest.xml") -Raw) -creplace '(<Identity [^>]*?)Version="[0-9.]+"', "`$1Version=`"$Version`""
[System.IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding $false))

# 3. Pack
$msix = Join-Path $out "Hangbar_${Version}_x64.msix"
& "$sdk\makeappx.exe" pack /d $stage /p $msix /o | Out-File (Join-Path $out "makeappx.log") -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw "makeappx failed (see out/makeappx.log)" }

# 4. Sign with a test certificate (created once in CurrentUser\My)
$pfx = Join-Path $out "hangbar-test.pfx"
$pw = ConvertTo-SecureString "hangbar-test" -AsPlainText -Force
if (-not (Test-Path $pfx)) {
  $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=HangbarSpike" -CertStoreLocation Cert:\CurrentUser\My `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") -NotAfter (Get-Date).AddYears(1)
  Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pw | Out-Null
  Export-Certificate -Cert $cert -FilePath (Join-Path $out "hangbar-test.cer") | Out-Null
}
& "$sdk\signtool.exe" sign /fd SHA256 /f $pfx /p "hangbar-test" $msix | Out-File (Join-Path $out "signtool.log") -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw "signtool failed (see out/signtool.log)" }
"built $msix"
