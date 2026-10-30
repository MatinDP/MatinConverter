# نتایج تست

آخرین بررسی روی Windows 11 x64 و Node.js 24:

- `npm run typecheck`: موفق
- `npm test`: موفق؛ ۴ تست موتور شامل PNG به JPEG واقعی، flatten آلفا، resize، ورودی خراب، جلوگیری از overwrite و خروجی AVIF
- `npm run build`: موفق
- `npm run dist:win`: موفق؛ Installer NSIS x64 ساخته شد
- `npm run smoke:packaged`: موفق؛ برنامه‌ی بسته‌بندی‌شده با تصویر فارسی‌نام اجرا شد و JPEG با ابعاد و فرمت مورد انتظار ساخت

تست‌های ویندوزی که هنوز انجام نشده‌اند: نصب و حذف NSIS، ساخت/ثبت MSIX Sparse، امضای MSIX، نمایش گزینه در منوی اصلی Windows 11، لغو Job از Explorer و رفتار Explorer بعد از Update/Repair. DLL منوی راست‌کلیک در محیط فعلی ساخته و وارد Installer نشده است.
