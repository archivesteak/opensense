; OpenSense's own installer text in Bulgarian, next to the app's Strings\bg-BG.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Bulgarian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Стартиране на OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Не е нужно да приемате този лиценз, за да използвате OpenSense. Той ви дава правото да разпространявате и променяте програмата и определя условията за това."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (задължително)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Драйвер PawnIO (задължително)"
; Component name.
${LangFileString} Inst_SecStartMenu "Пряк път в менюто „Старт“"
; Component name.
${LangFileString} Inst_SecDesktop "Пряк път на работния плот"
; Component name.
${LangFileString} Inst_SecAutostart "Стартиране с Windows"
; Component description.
${LangFileString} Inst_DescCore "Приложението OpenSense и неговата фонова услуга с всичко необходимо за работата им."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Подписан драйвер, чрез който OpenSense чете собствения температурен сензор на процесора, както ThrottleStop и HWiNFO. Пропуска се, ако PawnIO вече е инсталиран."
; Component description.
${LangFileString} Inst_DescStartMenu "Добавя OpenSense в менюто „Старт“."
; Component description.
${LangFileString} Inst_DescDesktop "Добавя пряк път към OpenSense на работния плот."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Отваря OpenSense в областта за уведомяване, когато влезете. Управлението на вентилаторите работи от стартирането във всеки случай."
; Progress log line.
${LangFileString} Inst_Closing "Затваряне на работещото копие на OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Премахване на инсталираната версия от $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Инсталираната версия на OpenSense не можа да бъде премахната (грешка $1). Да се продължи ли въпреки това?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 вече е инсталиран."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Инсталиране на PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Драйверът PawnIO не можа да се инсталира (грешка $1). Дотогава OpenSense ще показва по-неточната температура на ЦП от фърмуера."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Регистриране на услугата OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Услугата OpenSense не можа да бъде регистрирана (грешка $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Стартиране на услугата OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Услугата OpenSense не стартира (грешка $0). Журналът ѝ е в %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Управление на вентилатори, производителност и осветление за лаптопи Acer Nitro и Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Управление на вентилатори, производителност и осветление"
${LangFileString} Inst_Needs64Bit "OpenSense изисква 64-битова версия на Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense изисква Windows 10 версия 2004 (компилация ${MIN_WINDOWS_BUILD}) или по-нова."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Спиране на услугата OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Да се премахнат ли и вашите настройки и журнали на OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Някои файлове на OpenSense се използват и ще бъдат премахнати при рестартиране на Windows."
