param([Parameter(Mandatory)][string]$TrafficMonitorDir)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config.ps1')
$root = Split-Path $PSScriptRoot -Parent
$target = (Resolve-Path -LiteralPath $TrafficMonitorDir).Path
if (Get-Process TrafficMonitor -ErrorAction SilentlyContinue) { throw 'Please exit TrafficMonitor first. No files were changed.' }
$config = Join-Path $target 'config.ini'
$reader = [IO.StreamReader]::new($config, [Text.Encoding]::UTF8, $true)
try { $content = $reader.ReadToEnd(); $encoding = $reader.CurrentEncoding } finally { $reader.Dispose() }
$updated = Set-CodexQuotaLayout $content
$backup = Join-Path $root ('backups\layout-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $backup | Out-Null
Copy-Item -LiteralPath $config -Destination (Join-Path $backup 'config.ini')
$target | Set-Content -LiteralPath (Join-Path $backup 'target.txt') -Encoding utf8
[IO.File]::WriteAllText($config, $updated, $encoding)
Write-Output "Layout applied. Backup: $backup"
