# Validation

Automated core tests cover every advertised output encoder, PNG/JPEG/WEBP round-trips, alpha compositing, resize/enlargement, EXIF orientation, source protection, rename/skip/overwrite, per-source and shared destinations, Unicode paths, corrupt images, unavailable encoders, missing/blocked destination folders, multi-frame preservation, explicit first-frame extraction, cancellation and temporary-file cleanup, parallel name collisions, bounded thumbnails, settings recovery/persistence and independent job/quick defaults.

`MatinConverter.ShellTests` performs real Windows COM activation of the native bridge with real Shell item arrays. It submits 1, 10 and 100 files, repeated simultaneous requests, and a direct quick request through the existing worker. It checks output counts, absence of the main window and removal of registration after disabling. It temporarily saves/restores the app's registry keys and JSON and cleans its own generated fixtures. This exercises the same bridge used by Explorer; it is not a physical right-click test.

Last executed on the development Windows 11 x64 system: core 37/37 passed, including protecting another selected source and a hardlink alias from overwrite; Shell selections 1/10/100, simultaneous requests, named-pipe forwarding and registration removal passed. Dark RTL main/settings windows were visually reviewed. Final build and installer results are recorded in `artifacts/validation.txt` after execution.

`MatinConverter.UiTests` also passed against real WPF views: 100 thumbnails with actual list virtualization, removing/clearing selections, both languages and both themes, minimum-size layout, binding-error tracing, and manual conversion with completion/progress and re-enabled main window. The UI smoke test uses generated image fixtures and does not alter saved preferences.

An earlier development build of the installer was installed and uninstalled successfully on this account. Version 1.0.0's release build and self-contained publish passed. Run `scripts/verify-installer.ps1` to repeat install/uninstall checks against this exact release package.

## Physical test checklist (not claimed passed until executed)

- [ ] Clean supported Windows 10 x64 installation: run the EXE installer without admin; launch from Start Menu, check icon.
- [ ] Repeat on Windows 11 x64; locate classic command in Show more options.
- [ ] Explorer: right-click exactly 1, 10 and 100 PNG/JFIF/HEIC images with the main application closed; one progress window, all outputs in saved destination.
- [ ] Repeat quickly while a previous batch is active; requests queue without duplicate workers or overlapping progress windows.
- [ ] Main UI: drag/drop files and folders, add more, remove one, clear, recursive folders and unsupported file explanations.
- [ ] Light/dark/system and Persian/English; keyboard-only navigation and visible focus; 100/125/150/200 percent monitor DPI; smallest allowed window, long paths and text.
- [ ] Cancel during a large conversion, inspect completed outputs and verify no incomplete temporary output remains.
- [ ] Test real restricted ACLs, genuinely full disk, locked output, and delegate-dependent camera RAW fixtures. The automated blocked-path test does not simulate physical disk exhaustion or ACLs.
- [ ] Installer features: optional Desktop and Explorer integration, self-contained app with no development tools installed.
- [ ] Disable integration, verify menu disappears, then enable it again.
- [ ] Uninstall with integration enabled: all owned verb/CLSID keys, shortcuts and installed files are removed; user settings remain. Repeat while Explorer has loaded the bridge and honor any restart request.

No physical Windows 10 or clean VM installer/uninstaller result is inferred from the development-machine COM test.
