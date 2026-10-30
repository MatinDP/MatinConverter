# Matin Converter

ابزار دسکتاپ آفلاین برای تبدیل تصاویر در Windows با Electron، React، TypeScript و Sharp. رابط برنامه فارسی و راست‌چین است و فایل اصلی را تغییر نمی‌دهد.

## قابلیت‌های فعلی

- ورودی JPEG/JFIF، PNG، WebP، AVIF، TIFF، GIF و SVG؛ خروجی JPEG، PNG، WebP، AVIF، TIFF و GIF
- تبدیل چند فایل، تنظیم اندازه، کیفیت و سقف تقریبی حجم
- Flatten کردن شفافیت به رنگ انتخابی هنگام خروجی JPEG
- چرخش مطابق EXIF، حذف Metadata به‌صورت پیش‌فرض و نوشتن اتمی فایل خروجی
- تنظیمات محلی ماندگار و Quick Convert مستقل
- UI فارسی، پوسته روشن/تیره و اعلان پایان کار

## توسعه

نیازمند Node.js 20+ و npm است. `npm install` و سپس `npm run dev` را اجرا کنید.

## ساخت

- `npm run typecheck` بررسی TypeScript
- `npm test` تست‌ها
- `npm run build` ساخت برنامه
- `npm run dist:win` ساخت Installer ویندوز NSIS (روی Windows x64)
- `npm run native:win` پیکربندی CMake برای افزونه Explorer (نیازمند Visual Studio C++ Build Tools و Windows SDK)

این نسخه هنوز Shell Extension و MSIX Sparse Package قابل نصب ندارد؛ بنابراین تبدیل راست‌کلیک در Explorer فعال نیست. Installer تا وقتی Native target به تنظیمات electron-builder افزوده نشود فقط برنامه Electron را می‌بندد.

## حریم خصوصی

تصاویر محلی پردازش می‌شوند. برنامه برای پردازش نیاز به اینترنت ندارد و فونت‌های سیستمی استفاده می‌شوند.
