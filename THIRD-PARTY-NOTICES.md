# Third-party notices

The self-contained application includes .NET / WPF, Magick.NET and the native ImageMagick codecs distributed with that package. It does not require downloading codecs or running a server after installation.

- [.NET runtime](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) and [WPF](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT): MIT. Runtime third-party notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- [Magick.NET](https://github.com/dlemstra/Magick.NET/blob/main/License.txt): Apache License 2.0. Bundled native components and codec license notices: https://github.com/dlemstra/Magick.Native
- [ImageMagick](https://imagemagick.org/script/license.php): ImageMagick License. Native codec dependencies retain their respective licenses as documented by the upstream build.
- [PDFiumCore](https://github.com/Dtronix/PDFiumCore): Apache License 2.0. PDF pages are rendered using the bundled [PDFium binaries](https://github.com/bblanchon/pdfium-binaries), distributed under PDFium's BSD license and the licenses of its bundled dependencies. Copies are included in `Licenses/PDFium`.
- The small Shell bridge is built using the [MinGW-w64 runtime and headers](https://github.com/mingw-w64/mingw-w64/blob/master/COPYING), which include permissive notices and public-domain components. Zig is a build tool and is not shipped with the application.
- The installer uses [Inno Setup](https://jrsoftware.org/isinfo.php), copyright Jordan Russell and Martijn Laan. Its compiler is a build tool and is not bundled with the application; the installer retains the upstream notices.

Full copies of relevant upstream notices are included in `Licenses` alongside the application when published by the build script.
