param([string]$Zig = $env:ZIG_EXE)
$ErrorActionPreference = 'Stop'
if (-not $Zig) { $Zig = Join-Path $env:USERPROFILE '.local\share\MatinBuildTools\zig-x86_64-windows-0.15.2\zig.exe' }
if (-not (Test-Path -LiteralPath $Zig)) { throw 'Set ZIG_EXE to Zig 0.15.2 zig.exe. See README for the verified download.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot 'artifacts\native'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $Zig cc -shared -target x86_64-windows-gnu -O2 -Wall -Wextra (Join-Path $projectRoot 'native\ShellBridge.c') (Join-Path $projectRoot 'native\ShellBridge.def') -lole32 -lshell32 -luuid -o (Join-Path $output 'MatinConverter.Shell.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native Shell bridge build failed.' }
