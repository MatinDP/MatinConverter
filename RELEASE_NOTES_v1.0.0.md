# Matin Converter 1.0.0

First public release of the C#/.NET 10 WPF rewrite.

- Offline image conversion with JPEG/JFIF, PNG, WEBP, AVIF, BMP, TIFF, GIF, ICO and additional available formats.
- Persian RTL and English LTR interface, light/dark themes, persistent settings, quality and resize controls, bounded target file size, metadata handling and animated image policies.
- Manual selection, folder import, drag-and-drop and batch conversion.
- Optional classic Windows Explorer command with saved profiles, multi-selection and a separate progress window.
- Self-contained Windows x64 installer; no .NET or Node.js installation is required.

Windows 11 places the classic context-menu entry under **Show more options**. Windows 10 Explorer validation was not performed for this release. The installer is unsigned, and the image engine uses 8-bit Q8 processing.

## Verification

- Release build completed with 0 warnings and 0 errors.
- 41/41 core and settings tests passed.
- Native DLL compiled and the installer EXE was generated.
- Physical Explorer right-click and clean-VM install validation remain unverified for this release build.
