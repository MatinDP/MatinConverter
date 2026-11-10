$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$installer = Join-Path $projectRoot "artifacts\Matin-Converter-Setup-$version.exe"
$target = Join-Path $env:LOCALAPPDATA 'Programs\Matin Converter'
$exe = Join-Path $target 'MatinConverter.exe'
$developmentExe = Join-Path $projectRoot "artifacts\app-$version\MatinConverter.exe"
$key = 'Software\Classes\CLSID\{8B71E20D-9E6A-4936-9065-2D7E63937BF0}\InprocServer32'
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (Test-Path -LiteralPath $target) { throw 'An installation already exists. Run this test in a clean user profile.' }
if (-not (Test-Path $installer)) { throw 'Build the installer before running this test.' }
$settings = Join-Path $env:LOCALAPPDATA 'MatinConverter\settings.json'
$original = if (Test-Path $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
$oldRegistration = $registry.OpenSubKey($key)
$originalBridge = if ($oldRegistration) { $oldRegistration.GetValue(''); $oldRegistration.Dispose() } else { $null }
if ($originalBridge -and $originalBridge -ne (Join-Path (Split-Path $developmentExe) 'MatinConverter.Shell.dll')) {
    $registry.Dispose()
    throw 'A different application copy owns the context menu. Use a clean profile for this test.'
}
$testRoot = Join-Path $projectRoot 'artifacts\installer-test'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

function Invoke-Wait([string]$file, [string[]]$arguments, [int]$timeout = 120000) {
    $start = [Diagnostics.ProcessStartInfo]::new($file)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit($timeout)) { $process.Kill(); $process.WaitForExit(); throw "Process timed out: $file" }
        if ($process.ExitCode -notin @(0, 3010)) { throw "Process failed ($($process.ExitCode)): $file" }
    } finally { $process.Dispose() }
}
function Install-App([string]$tasks, [string]$log) {
    Invoke-Wait $installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/SP-', '/NORESTART', '/NOCLOSEAPPLICATIONS', "/TASKS=$tasks", "/LOG=$log")
}
function Uninstall-App([string]$log) {
    Invoke-Wait (Join-Path $target 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$log")
}
function Assert-NoShell {
    $keys = @('Software\Classes\CLSID\{8B71E20D-9E6A-4936-9065-2D7E63937BF0}')
    $extensionManifest = Get-Content (Join-Path $projectRoot 'packaging/image-extensions.iss') -Raw
    if ($extensionManifest -notmatch '#define Extensions "([^"]+)"') { throw 'Invalid Shell extension manifest.' }
    foreach ($ext in $Matches[1].Split('|')) {
        $keys += "Software\Classes\SystemFileAssociations\$ext\shell\MatinConverter"
    }
    foreach ($path in $keys) {
        $probe = $registry.OpenSubKey($path)
        if ($probe) { $probe.Dispose(); throw "Shell registration remained: $path" }
    }
}
$installed = $false
try {
    Invoke-Wait $developmentExe @('--unregister')
    Install-App 'shell,desktopicon' (Join-Path $testRoot 'install.log')
    $installed = $true
    foreach ($file in @('MatinConverter.exe', 'MatinConverter.Shell.dll', 'coreclr.dll', 'PresentationFramework.dll', 'Magick.Native-Q8-x64.dll', 'policy.xml')) {
        if (-not (Test-Path (Join-Path $target $file))) { throw "Missing installed file: $file" }
    }
    $registered = $registry.OpenSubKey($key)
    if ($null -eq $registered) { throw 'Installer Shell registration is missing.' }
    try { if ($registered.GetValue('') -ne (Join-Path $target 'MatinConverter.Shell.dll')) { throw 'Installer registered the wrong Shell path.' } } finally { $registered.Dispose() }
    $desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Matin Converter.lnk'
    $startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Matin Converter.lnk'
    if (-not ((Test-Path $desktop) -and (Test-Path $startMenu))) { throw 'Installer shortcuts are missing.' }
    $fixture = Join-Path $testRoot 'sample.png'
    Copy-Item (Join-Path $projectRoot 'assets\icon.png') $fixture -Force
    $output = Join-Path $testRoot ('output-' + [Guid]::NewGuid().ToString('N'))
    $profile = @{SchemaVersion = 1; Defaults = @{Format = 'Jpeg'; Quality = 85; Destination = 'Custom'; OutputDirectory = $output }; QuickUsesDefaults = $true; Notify = $false; Language = 'fa' } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($settings, $profile)
    Invoke-Wait $exe @('--quick', $fixture) 45000
    if (-not (Test-Path (Join-Path $output 'sample.jpg'))) { throw 'The installed application failed to convert an image.' }
    Write-Output 'PASS: EXE installation, bundled runtime/native files, shortcuts, Shell registration and installed quick conversion.'
    & $dotnet run --project (Join-Path $projectRoot 'tests\MatinConverter.ShellTests') -c Release --no-build -- $exe
    if ($LASTEXITCODE -ne 0) { throw 'Installed Shell integration tests failed.' }
    Uninstall-App (Join-Path $testRoot 'uninstall.log')
    $installed = $false
    if ((Test-Path $exe) -or (Test-Path $desktop) -or (Test-Path $startMenu)) { throw 'Installed files or shortcuts remained after uninstall.' }
    Assert-NoShell
    if (-not (Test-Path $settings)) { throw 'Uninstall removed user preferences.' }
    Write-Output 'PASS: uninstall removes application, shortcuts and every owned Shell key while preserving preferences.'
    Install-App '' (Join-Path $testRoot 'install-without-tasks.log')
    $installed = $true
    Assert-NoShell
    if (Test-Path $desktop) { throw 'An unselected desktop shortcut was created.' }
    Invoke-Wait $exe @('--register')
    Uninstall-App (Join-Path $testRoot 'uninstall-after-enable.log')
    $installed = $false
    Assert-NoShell
    Write-Output 'PASS: optional integration/desktop can be omitted; uninstall also removes integration enabled later in settings.'
} finally {
    if ($installed -and (Test-Path (Join-Path $target 'unins000.exe'))) { Uninstall-App (Join-Path $testRoot 'cleanup.log') }
    if ($originalBridge) { Invoke-Wait $developmentExe @('--register') }
    if ($null -ne $original) { [IO.File]::WriteAllBytes($settings, $original) } elseif (Test-Path $settings) { [IO.File]::Delete($settings) }
    $registry.Dispose()
}
