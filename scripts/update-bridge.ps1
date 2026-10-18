param([Parameter(Mandatory)][string]$TrafficMonitorDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path (Resolve-Path -LiteralPath $TrafficMonitorDir).Path 'plugins\CodexQuotaBridge'
if (Get-Process TrafficMonitor -ErrorAction SilentlyContinue) { throw 'Please exit TrafficMonitor first. No files were changed.' }
for ($attempt = 0; $attempt -lt 20 -and (Get-Process CodexQuotaBridge -ErrorAction SilentlyContinue); $attempt++) { Start-Sleep -Milliseconds 500 }
if (Get-Process CodexQuotaBridge -ErrorAction SilentlyContinue) { throw 'The previous bridge is still running.' }
if (!(Test-Path -LiteralPath $target)) { throw 'Run install.ps1 for first installation.' }
$source = Join-Path $root 'dist\CodexQuotaBridge'
if (!(Test-Path -LiteralPath (Join-Path $source 'CodexQuotaBridge.exe'))) { throw 'Build the bridge first.' }
$backup = Join-Path $root ('backups\bridge-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $backup | Out-Null
Copy-Item -LiteralPath $target -Destination $backup -Recurse
Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $target -Force
Write-Output "Bridge updated; taskbar layout preserved. Backup: $backup"
