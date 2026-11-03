param([switch]$Installer,[switch]$ShellTests,[switch]$DesktopTests,[string]$PublishDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$dotnet=Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if(-not(Test-Path $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location $projectRoot
try {
 & "$PSScriptRoot\build-native.ps1"
 & $dotnet build MatinConverter.sln -c Release
 if($LASTEXITCODE -ne 0){throw 'Build failed.'}
 & $dotnet test tests/MatinConverter.Tests -c Release --no-build
 if($LASTEXITCODE -ne 0){throw 'Core tests failed.'}
 if($DesktopTests){
  & $dotnet run --project tests/MatinConverter.UiTests -c Release --no-build
  if($LASTEXITCODE -ne 0){throw 'WPF UI tests failed.'}
 }
 if($ShellTests){& $dotnet run --project tests/MatinConverter.ShellTests -c Release -- (Join-Path $projectRoot 'src\MatinConverter.Windows\bin\Release\net10.0-windows\win-x64\MatinConverter.exe');if($LASTEXITCODE -ne 0){throw 'Shell tests failed.'}}
 $version=([xml](Get-Content (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
 $publish=if($PublishDirectory){[IO.Path]::GetFullPath($PublishDirectory)}else{Join-Path $projectRoot "artifacts\app-$version"}
 & $dotnet publish src/MatinConverter.Windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o $publish
 if($LASTEXITCODE -ne 0){throw 'Publish failed.'}
 Copy-Item (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') $publish
 if($Installer){
  $compiler=$env:INNO_ISCC
  if(-not $compiler){$compiler=Join-Path $env:USERPROFILE '.local\share\MatinBuildTools\InnoSetup-7.1.0\tools\ISCC.exe'}
  if(-not(Test-Path -LiteralPath $compiler)){throw 'Set INNO_ISCC to Inno Setup 7.1 ISCC.exe. See README.'}
  & $compiler /Qp "/DProjectDir=$projectRoot" "/DPublishDir=$publish" "/DAppVersion=$version" packaging/MatinConverter.iss
  if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
 }
} finally {Pop-Location}
