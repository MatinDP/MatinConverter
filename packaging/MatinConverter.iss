#ifndef ProjectDir
  #define ProjectDir ".."
#endif
#ifndef PublishDir
  #define PublishDir ProjectDir + "\artifacts\app"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define ShellId "{{8B71E20D-9E6A-4936-9065-2D7E63937BF0}"
#define Extensions ".jpg|.jpeg|.jfif|.png|.webp|.avif|.heic|.heif|.gif|.bmp|.tif|.tiff|.ico|.svg|.svgz|.psd|.tga|.jp2|.dng|.cr2|.cr3|.nef|.arw|.orf|.raf|.rw2"

[Setup]
AppId={{A203FA13-CF07-4B8C-A74B-029392FA11EA}
AppName=Matin Converter
AppVersion={#AppVersion}
AppPublisher=MatinDP
AppPublisherURL=https://github.com/MatinDP/MatinConverter
DefaultDirName={localappdata}\Programs\Matin Converter
DefaultGroupName=Matin Converter
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
OutputDir={#ProjectDir}\artifacts
OutputBaseFilename=Matin-Converter-Setup-{#AppVersion}
SetupIconFile={#ProjectDir}\assets\icon.ico
UninstallDisplayIcon={app}\MatinConverter.exe
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Tasks]
Name: "shell"; Description: "Add Convert with Matin Converter to the classic Explorer context menu"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Matin Converter"; Filename: "{app}\MatinConverter.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Matin Converter"; Filename: "{app}\MatinConverter.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#ShellId}"; Flags: dontcreatekey uninsdeletekey
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#ShellId}"; ValueType: string; ValueData: "Matin Converter Shell Command"; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#ShellId}\InprocServer32"; ValueType: string; ValueData: "{app}\MatinConverter.Shell.dll"; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#ShellId}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"; Tasks: shell
#define ExtensionIndex 0
#sub ExtensionRegistry
  #define Extension Copy(Extensions, ExtensionIndex + 1, Pos("|", Copy(Extensions + "|", ExtensionIndex + 1)) - 1)
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter"; Flags: dontcreatekey uninsdeletekey
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter\command"; Flags: deletekey; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter"; ValueType: string; ValueData: "Convert with Matin Converter"; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Convert with Matin Converter"; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\MatinConverter.exe"",0"; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shell
Root: HKCU64; Subkey: "Software\Classes\SystemFileAssociations\{#Extension}\shell\MatinConverter"; ValueType: string; ValueName: "ExplorerCommandHandler"; ValueData: "{#ShellId}"; Tasks: shell
  #expr ExtensionIndex = ExtensionIndex + Len(Extension) + 1
#endsub
#for {0; ExtensionIndex < Len(Extensions); 0} ExtensionRegistry

[Run]
Filename: "{app}\MatinConverter.exe"; Description: "Open Matin Converter"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\MatinConverter.exe"; Parameters: "--unregister"; Flags: runhidden waituntilterminated skipifdoesntexist

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Arguments: String;
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('shell') then Arguments := '--register'
    else Arguments := '--unregister';
    if not Exec(ExpandConstant('{app}\MatinConverter.exe'), Arguments,
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Unable to configure Explorer integration.');
    if ResultCode <> 0 then
      RaiseException('Explorer integration could not be verified.');
  end;
end;
