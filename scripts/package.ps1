param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.2.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($file in @('dist/CodexQuota.dll', 'dist/CodexQuotaBridge/CodexQuotaBridge.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $root $file))) { throw 'Run scripts/build.ps1 first.' }
}
$release = Join-Path $root 'release'
New-Item -ItemType Directory -Force $release | Out-Null
$stage = Join-Path $release ('stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
foreach ($name in @('dist', 'vendor/TrafficMonitor', 'scripts')) {
    New-Item -ItemType Directory -Path (Join-Path $stage $name) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $root 'dist/CodexQuota.dll') -Destination (Join-Path $stage 'dist')
Copy-Item -LiteralPath (Join-Path $root 'dist/CodexQuotaBridge') -Destination (Join-Path $stage 'dist') -Recurse
foreach ($name in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md','CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $root 'vendor/TrafficMonitor/LICENSE') -Destination (Join-Path $stage 'vendor/TrafficMonitor')
foreach ($name in @('install.ps1','update-plugin.ps1','apply-layout.ps1','config.ps1','set-refresh.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'scripts')
}
$zip = Join-Path $release "CodexQuota-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
Write-Output $zip
Write-Output "$zip.sha256"
