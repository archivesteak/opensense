; OpenSense's own installer text in Farsi, next to the app's Strings\fa-IR.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Farsi"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "شروع OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "برای استفاده از OpenSense نیازی به پذیرفتن این مجوز نیست. این مجوز به شما حق اشتراک‌گذاری و تغییر آن را می‌دهد و شرایط انجام این کار را تعیین می‌کند."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (الزامی)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "درایور PawnIO (الزامی)"
; Component name.
${LangFileString} Inst_SecStartMenu "میان‌بر منوی شروع"
; Component name.
${LangFileString} Inst_SecDesktop "میان‌بر میزکار"
; Component name.
${LangFileString} Inst_SecAutostart "شروع همراه با Windows"
; Component description.
${LangFileString} Inst_DescCore "برنامهٔ OpenSense و سرویس پس‌زمینهٔ آن، با هر آنچه برای اجرا لازم دارند."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "درایور امضاشده‌ای که به OpenSense امکان می‌دهد حسگر دمای خود CPU را بخواند، مانند ThrottleStop و HWiNFO. اگر PawnIO از قبل نصب باشد، رد می‌شود."
; Component description.
${LangFileString} Inst_DescStartMenu "OpenSense را به منوی شروع اضافه می‌کند."
; Component description.
${LangFileString} Inst_DescDesktop "یک میان‌بر OpenSense به میزکار اضافه می‌کند."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "هنگام ورود به سیستم، OpenSense را در ناحیهٔ اعلان باز می‌کند. کنترل فن در هر صورت از زمان راه‌اندازی اجرا می‌شود."
; Progress log line.
${LangFileString} Inst_Closing "در حال بستن OpenSense در حال اجرا..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "در حال حذف نسخهٔ نصب‌شده از $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "نسخهٔ نصب‌شدهٔ OpenSense حذف نشد (خطای $1). با این حال ادامه داده شود؟"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 از قبل نصب است."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "در حال نصب PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "درایور PawnIO نصب نشد (خطای $1). تا زمانی که نصب شود، OpenSense دمای کم‌دقت‌تر CPU را از سفت‌افزار نشان می‌دهد."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "در حال ثبت سرویس OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "سرویس OpenSense ثبت نشد (خطای $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "در حال شروع سرویس OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "سرویس OpenSense شروع نشد (خطای $0). گزارش آن در %ProgramData%\OpenSense\logs است."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "کنترل فن، عملکرد و نورپردازی برای لپ‌تاپ‌های Acer Nitro و Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "کنترل فن، عملکرد و نورپردازی"
${LangFileString} Inst_Needs64Bit "OpenSense به Windows ۶۴ بیتی نیاز دارد."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense به Windows 10 نسخهٔ 2004 (ساخت ${MIN_WINDOWS_BUILD}) یا جدیدتر نیاز دارد."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "در حال توقف سرویس OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "تنظیمات و گزارش‌های OpenSense شما هم حذف شوند؟"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "برخی از فایل‌های OpenSense در حال استفاده هستند و هنگام راه‌اندازی مجدد Windows حذف می‌شوند."
