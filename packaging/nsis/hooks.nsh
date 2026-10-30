!macro customUnInstall
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.jpg\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.jpeg\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.jpe\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.jfif\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.png\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.webp\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.avif\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.tif\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.tiff\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.gif\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.svg\shell\MatinConverter"
  DeleteRegKey HKCU "Software\Classes\SystemFileAssociations\.svgz\shell\MatinConverter"
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
!macroend
