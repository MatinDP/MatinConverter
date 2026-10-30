# Architecture

- `src/renderer`: رابط React؛ دسترسی سیستم‌عامل فقط از API محدود preload.
- `src/preload`: پل typed به چند عملیات allow-listed در IPC.
- `src/main`: پنجره‌ها، تنظیمات، انتخاب مسیر، اعلان و مدیریت Jobهای سریالی.
- `src/processing/engine.ts`: تبدیل Sharp، resize، کیفیت هدف محدود به 7 تلاش، خروجی موقت و rename اتمی.
- `src/shared`: مدل‌ها و مقادیر پیش‌فرض.

تنظیمات با electron-store در پوشه userData ذخیره می‌شود. Quick Convert در `settings.quick` مستقل از تنظیمات تبدیل جاری است. فایل‌های خروجی suffix می‌گیرند و هیچ‌گاه روی ورودی نوشته نمی‌شوند.

پوشه `native/shell-extension` شامل سورس COM مبتنی بر `IExplorerCommand` و قالب manifest برای Sparse MSIX است. در خط ساخت فعلی DLL ساخته، کپی یا ثبت نمی‌شود و Installer نیز Package Identity hook ندارد؛ تا تکمیل و اعتبارسنجی این مسیر، IPC status عمداً unavailable است. مسیر `Invoke` برنامه Electron هم‌جوار DLL را اجرا و مسیرهای Unicode را با quoting امن به آن می‌دهد.

برای سازگاری بدون گواهی، تنظیمات برنامه می‌تواند منوی کلاسیک کاربر فعلی را از طریق `reg.exe` با آرگومان‌های جداگانه و بدون shell فعال/غیرفعال کند. این گزینه به برنامه‌ی بسته‌بندی‌شده نیاز دارد؛ در ویندوز ۱۱ فقط در «نمایش گزینه‌های بیشتر» ظاهر می‌شود. NSIS hook کلیدهای اختصاصی برنامه را هنگام حذف پاک می‌کند.
