# Installer

`npm run dist:win` هدف NSIS x64 را می‌سازد. برای انتشار عمومی، فایل نصب باید با گواهی Code Signing معتبر Authenticode امضا شود؛ کلید خصوصی یا گواهی را در repository نگه ندارید. نصب‌کننده فعلی هنوز registration/unregistration مربوط به Sparse MSIX را انجام نمی‌دهد چون Native extension در این نسخه در دسترس نیست.
