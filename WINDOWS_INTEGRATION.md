# Windows Explorer integration status

وضعیت فعلی: سورس COM بومی و قالب Sparse MSIX آماده شده‌اند، اما DLL ساخته، بسته‌بندی، ثبت یا آزمایش نشده است. هیچ Registry Verb جایگزین با ادعای حضور در منوی مدرن استفاده نشده است.

برای فعال‌سازی بدون گواهی، برنامه حالا یک مسیر جداگانه‌ی منوی کلاسیک دارد: از تنظیمات برنامه، «فعال‌سازی منو» کلیدهای اختصاصی کاربر فعلی را برای پسوندهای تصویر ثبت می‌کند و `MultiSelectModel=Player` را برای انتخاب چندتایی می‌گذارد. این مسیر در Windows 10 نمایش داده می‌شود؛ در Windows 11 فقط زیر «نمایش گزینه‌های بیشتر» دیده می‌شود. برای ظاهر شدن در منوی اصلی Windows 11 همچنان مسیر IExplorerCommand و Package Identity لازم است. حذف برنامه نیز کلیدهای اختصاصی Matin Converter را پاک می‌کند.

برای تکمیل باید یک DLL x64 با COM class ثابت و `IExplorerCommand` ساخته شود، فایل‌های `IShellItemArray` فقط با استخراج مسیر Unicode امن دریافت شوند، و `windows.comServer` همراه `windows.fileExplorerContextMenus` در Sparse MSIX manifest تعریف شود. DLL باید کار سنگین نکند و فقط آرگومان‌ها را به launcher تحویل دهد.

CLSID افزونه `7F036A93-6017-4F13-A85B-A2CA2D538D41` است. ساخت منو فقط extension و ویژگی پوشه را بررسی می‌کند؛ پردازش بعد از `Invoke` شروع می‌شود. برای فعال‌شدن مسیر Installer باید DLL و دارایی‌های manifest به محل نصب خارجی کپی شوند، Package Identity به فایل اجرایی Electron افزوده شود، Sparse MSIX با گواهی هم‌نام Publisher امضا و ثبت شود و مراحل ثبت/حذف همراه گزارش خطا به NSIS اضافه شوند. ساخت نیازمند Windows SDK و MSVC C++ Build Tools است. در محیط فعلی `cmake` و `cl` نصب نبودند؛ بنابراین هیچ تستی برای Modern Context Menu ادعا نمی‌شود. ثبت، امضا و refresh/restart Explorer نیز به Windows واقعی نیاز دارند.
