$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $zig = Join-Path $root 'tools/zig-x86_64-windows-0.15.2/zig.exe'
    if (!(Test-Path $zig)) { throw 'Run scripts/bootstrap.ps1 first.' }
    New-Item -ItemType Directory -Force build,dist | Out-Null
    & $zig c++ -target x86_64-windows-msvc -nostdinc -nostdlib -c src/Plugin/Plugin.cpp -o build/Plugin.obj -O2 -fno-exceptions -fno-rtti -fno-stack-protector -fno-threadsafe-statics
    if ($LASTEXITCODE) { throw 'Plugin compilation failed' }
    & $zig build-lib build/Plugin.obj -target x86_64-windows-gnu -dynamic -lkernel32 -luser32 '-fentry=DllMain' '-femit-bin=dist/CodexQuota.dll' -O ReleaseSmall
    if ($LASTEXITCODE) { throw 'Plugin linking failed' }
    dotnet publish src/QuotaBridge/QuotaBridge.csproj -c Release -r win-x64 --self-contained false -o dist/CodexQuotaBridge
    if ($LASTEXITCODE) { throw 'Bridge build failed' }
} finally { Pop-Location }
