; OpenSense's own installer text in Ukrainian, next to the app's Strings\uk-UA.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Ukrainian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Запустити OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Щоб користуватися OpenSense, не потрібно приймати цю ліцензію. Вона надає вам право поширювати та змінювати програму й визначає умови, за яких це можна робити."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (обов'язково)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Драйвер PawnIO (обов'язково)"
; Component name.
${LangFileString} Inst_SecStartMenu "Ярлик у меню «Пуск»"
; Component name.
${LangFileString} Inst_SecDesktop "Ярлик на робочому столі"
; Component name.
${LangFileString} Inst_SecAutostart "Запускати разом із Windows"
; Component description.
${LangFileString} Inst_DescCore "Програма OpenSense і її фонова служба з усім необхідним для роботи."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Підписаний драйвер, за допомогою якого OpenSense зчитує власний датчик температури процесора, як ThrottleStop і HWiNFO. Пропускається, якщо PawnIO вже інстальовано."
; Component description.
${LangFileString} Inst_DescStartMenu "Додає OpenSense до меню «Пуск»."
; Component description.
${LangFileString} Inst_DescDesktop "Додає ярлик OpenSense на робочий стіл."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Відкриває OpenSense в області сповіщень під час входу. Керування вентиляторами в будь-якому разі працює від запуску системи."
; Progress log line.
${LangFileString} Inst_Closing "Закриття запущеного OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Видалення інстальованої версії з $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Не вдалося видалити інстальовану версію OpenSense (помилка $1). Усе одно продовжити?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 вже інстальовано."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Інсталяція PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Не вдалося інсталювати драйвер PawnIO (помилка $1). Доти OpenSense показуватиме менш точну температуру ЦП від мікропрограми."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Реєстрація служби OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Не вдалося зареєструвати службу OpenSense (помилка $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Запуск служби OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Служба OpenSense не запустилася (помилка $0). Її журнал розташовано в папці %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Керування вентиляторами, продуктивністю й підсвічуванням ноутбуків Acer Nitro і Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Керування вентиляторами, продуктивністю й підсвічуванням"
${LangFileString} Inst_Needs64Bit "Для OpenSense потрібна 64-розрядна версія Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "Для OpenSense потрібна Windows 10 версії 2004 (збірка ${MIN_WINDOWS_BUILD}) або новішої."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Зупинка служби OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Видалити також ваші настройки й журнали OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Деякі файли OpenSense використовуються й будуть видалені після перезапуску Windows."
