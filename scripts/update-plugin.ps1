param([Parameter(Mandatory)][string]$TrafficMonitorDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path (Resolve-Path -LiteralPath $TrafficMonitorDir).Path 'plugins'
if (Get-Process TrafficMonitor -ErrorAction SilentlyContinue) { throw 'Please exit TrafficMonitor first. No files were changed.' }
for ($attempt = 0; $attempt -lt 20 -and (Get-Process CodexQuotaBridge -ErrorAction SilentlyContinue); $attempt++) { Start-Sleep -Milliseconds 500 }
if (Get-Process CodexQuotaBridge -ErrorAction SilentlyContinue) { throw 'Close the quota settings window and wait for the previous bridge to exit.' }
if (!(Test-Path -LiteralPath "$target\CodexQuota.dll")) { throw 'Run install.ps1 for first installation.' }
$source = Join-Path $root 'dist'
if (!(Test-Path -LiteralPath "$source\CodexQuota.dll") -or !(Test-Path -LiteralPath "$source\CodexQuotaBridge\CodexQuotaBridge.exe")) { throw 'Build first.' }
$backup = Join-Path $root ('backups\plugin-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $backup | Out-Null
Copy-Item -LiteralPath "$target\CodexQuota.dll" -Destination $backup
Copy-Item -LiteralPath "$target\CodexQuotaBridge" -Destination $backup -Recurse
try {
    Copy-Item -LiteralPath "$source\CodexQuota.dll" -Destination "$target\CodexQuota.dll" -Force
    Get-ChildItem -LiteralPath "$source\CodexQuotaBridge" -File | Copy-Item -Destination "$target\CodexQuotaBridge" -Force
} catch {
    Copy-Item -LiteralPath "$backup\CodexQuota.dll" -Destination "$target\CodexQuota.dll" -Force
    Get-ChildItem -LiteralPath "$backup\CodexQuotaBridge" -File | Copy-Item -Destination "$target\CodexQuotaBridge" -Force
    throw
}
Write-Output "Plugin and bridge updated; all settings preserved. Backup: $backup"
