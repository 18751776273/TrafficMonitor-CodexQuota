param([Parameter(Mandatory)][string]$TrafficMonitorDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'config.ps1')
$target = (Resolve-Path -LiteralPath $TrafficMonitorDir).Path
if (!(Test-Path -LiteralPath (Join-Path $target 'TrafficMonitor.exe'))) { throw 'TrafficMonitor.exe not found.' }
if (Get-Process TrafficMonitor -ErrorAction SilentlyContinue) { throw 'Please exit TrafficMonitor first. No files were changed.' }
if (Get-Process CodexQuotaBridge -ErrorAction SilentlyContinue) { throw 'Close quota settings and wait for the previous bridge to exit.' }
$exe = [IO.File]::ReadAllBytes((Join-Path $target 'TrafficMonitor.exe'))
$pe = [BitConverter]::ToInt32($exe, 60)
if ([BitConverter]::ToUInt16($exe, $pe + 4) -ne 0x8664) { throw 'This plugin requires x64 TrafficMonitor.' }
$dist = Join-Path $root 'dist'
if (!(Test-Path "$dist\CodexQuota.dll") -or !(Test-Path "$dist\CodexQuotaBridge\CodexQuotaBridge.exe")) { throw 'Run scripts/build.ps1 first.' }
$backup = Join-Path $root ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $backup | Out-Null
$config = Join-Path $target 'config.ini'
Copy-Item -LiteralPath $config -Destination (Join-Path $backup 'config.ini')
$plugins = Join-Path $target 'plugins'
foreach ($name in @('CodexQuota.dll', 'CodexQuotaBridge')) {
    $existing = Join-Path $plugins $name
    if (Test-Path -LiteralPath $existing) { Copy-Item -LiteralPath $existing -Destination $backup -Recurse }
}
$target | Set-Content -LiteralPath (Join-Path $backup 'target.txt') -Encoding utf8
$reader = [IO.StreamReader]::new($config, [Text.Encoding]::UTF8, $true)
try { $content = $reader.ReadToEnd(); $encoding = $reader.CurrentEncoding } finally { $reader.Dispose() }
$content = Set-CodexQuotaLayout $content
New-Item -ItemType Directory -Force $plugins | Out-Null
Copy-Item -LiteralPath "$dist\CodexQuota.dll" -Destination "$plugins\CodexQuota.dll" -Force
Copy-Item -LiteralPath "$dist\CodexQuotaBridge" -Destination $plugins -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'vendor\TrafficMonitor\LICENSE') -Destination "$plugins\CodexQuota-TrafficMonitor-LICENSE.txt" -Force
[IO.File]::WriteAllText($config, $content, $encoding)
Write-Output "Installed: $plugins\CodexQuota.dll"
Write-Output "Backup: $backup"
Write-Output 'Start TrafficMonitor. The bridge starts automatically; 5h and 7d show remaining percent.'
