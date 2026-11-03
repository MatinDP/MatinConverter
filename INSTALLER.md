# Installer

Build explicitly with `scripts/build.ps1 -Installer`. Ordinary builds do not recreate the installer. The result is `artifacts/Matin-Converter-Setup-1.0.0.exe`.

Inno Setup 7.1 compiles `packaging/MatinConverter.iss`; set `INNO_ISCC` to the compiler path. Use the signed compiler from the upstream distribution. This development build used portable Tools.InnoSetup 7.1.0 from NuGet, whose ISCC.exe signature was verified as valid (Pyrsys B.V.). The Inno Setup license is included under `Licenses`.

The package installs per user without elevation, under `%LOCALAPPDATA%\Programs\Matin Converter` by default. Windows x64 build 19041 or later is required. The complete .NET 10 runtime, WPF, Magick native library and Shell bridge are bundled. No development tools or internet connection are needed at runtime.

The classic Explorer command is selected by default; the Desktop shortcut is optional. Start Menu and uninstall entries use the application's embedded icon. Registry entries belong only to Matin Converter in the current user's class hive. The installed executable verifies integration after setup.

Uninstall through Windows Installed Apps or `unins000.exe`. It removes installed files, shortcuts and the owned Shell registrations, including integration enabled later from Settings. Preferences in `%LOCALAPPDATA%\MatinConverter` are retained. Files held by Explorer may need a restart to finish removal; the installer does not force a restart.

Run `scripts/verify-installer.ps1` only with no existing installation at the default destination. It temporarily installs and removes the app twice, exercises conversion and COM multi-selection, and restores the original preferences. See TESTING.md for executed checks and remaining clean-machine / Windows 10 validation.

This installer is unsigned. It does not replace or migrate the old Electron install; remove that old application separately before using this rewrite to avoid two entries.
