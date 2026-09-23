; OpenSense's own installer text in Arabic, next to the app's Strings\ar-SA.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Arabic"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "بدء تشغيل OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "لا تحتاج إلى قبول هذا الترخيص لاستخدام OpenSense. فهو يمنحك الحق في مشاركته وتعديله، ويحدد شروط القيام بذلك."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (مطلوب)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "برنامج تشغيل PawnIO (مطلوب)"
; Component name.
${LangFileString} Inst_SecStartMenu "اختصار في قائمة $\"ابدأ$\""
; Component name.
${LangFileString} Inst_SecDesktop "اختصار على سطح المكتب"
; Component name.
${LangFileString} Inst_SecAutostart "البدء مع Windows"
; Component description.
${LangFileString} Inst_DescCore "تطبيق OpenSense وخدمته التي تعمل في الخلفية، مع كل ما يحتاجان إليه للعمل."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "برنامج تشغيل موقّع يتيح لـ OpenSense قراءة مستشعر درجة الحرارة الخاص بـ CPU، مثل ThrottleStop وHWiNFO. يتم تخطيه إذا كان PawnIO مثبّتًا بالفعل."
; Component description.
${LangFileString} Inst_DescStartMenu "يضيف OpenSense إلى قائمة $\"ابدأ$\"."
; Component description.
${LangFileString} Inst_DescDesktop "يضيف اختصارًا لـ OpenSense إلى سطح المكتب."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "يفتح OpenSense في منطقة الإعلام عند تسجيل الدخول. يعمل التحكم في المراوح منذ بدء التشغيل في كل الأحوال."
; Progress log line.
${LangFileString} Inst_Closing "جارٍ إغلاق نسخة OpenSense قيد التشغيل..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "جارٍ إزالة الإصدار المثبّت من $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "تعذّرت إزالة الإصدار المثبّت من OpenSense (الخطأ $1). هل تريد المتابعة على أي حال؟"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 مثبّت بالفعل."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "جارٍ تثبيت PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "تعذّر تثبيت برنامج تشغيل PawnIO (الخطأ $1). إلى أن يتم تثبيته، سيعرض OpenSense درجة حرارة CPU الأقل دقة من البرنامج الثابت."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "جارٍ تسجيل خدمة OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "تعذّر تسجيل خدمة OpenSense (الخطأ $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "جارٍ بدء خدمة OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "لم تبدأ خدمة OpenSense (الخطأ $0). يوجد سجلها في %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "التحكم في المراوح والأداء والإضاءة لأجهزة الكمبيوتر المحمولة Acer Nitro وPredator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "التحكم في المراوح والأداء والإضاءة"
${LangFileString} Inst_Needs64Bit "يتطلب OpenSense إصدار Windows بنظام 64 بت."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "يتطلب OpenSense نظام Windows 10 الإصدار 2004 (البنية ${MIN_WINDOWS_BUILD}) أو أحدث."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "جارٍ إيقاف خدمة OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "هل تريد أيضًا إزالة إعدادات OpenSense وسجلاته؟"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "بعض ملفات OpenSense قيد الاستخدام وستتم إزالتها عند إعادة تشغيل Windows."
