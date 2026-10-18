$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$toolDir = Join-Path $root 'tools'
New-Item -ItemType Directory -Force $toolDir | Out-Null
$package = (Invoke-RestMethod 'https://ziglang.org/download/index.json').'0.15.2'.'x86_64-windows'
$zip = Join-Path $toolDir 'zig.zip'
if (!(Test-Path $zip)) { Invoke-WebRequest $package.tarball -OutFile $zip }
if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $package.shasum) { throw 'Zig SHA256 mismatch' }
Expand-Archive -LiteralPath $zip -DestinationPath $toolDir -Force
Write-Output 'Portable Zig ready. .NET 9 SDK is also required.'
