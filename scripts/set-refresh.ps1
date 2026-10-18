param([Parameter(Mandatory)][ValidateRange(30,300)][int]$Seconds)
$ErrorActionPreference = 'Stop'
$stateDir = if ($env:CODEX_QUOTA_STATE_DIR) { $env:CODEX_QUOTA_STATE_DIR } else { Join-Path $env:LOCALAPPDATA 'CodexQuota' }
New-Item -ItemType Directory -Force $stateDir | Out-Null
$path = Join-Path $stateDir 'settings.json'
$tempPath = Join-Path $stateDir ('settings-' + [guid]::NewGuid().ToString('N') + '.tmp')
$settings = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
if ($settings -isnot [System.Collections.IDictionary]) { throw 'Settings must be a JSON object.' }
$settings['refreshSeconds'] = $Seconds
[IO.File]::WriteAllText($tempPath, ($settings | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
[IO.File]::Move($tempPath, $path, $true)
Write-Output "Refresh interval: $Seconds seconds. The updated bridge reloads this setting within one second."
