; OpenSense's own installer text in Russian, next to the app's Strings\ru-RU.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Russian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Запустить OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Для использования OpenSense не нужно принимать эту лицензию. Она даёт вам право распространять и изменять программу и определяет условия, на которых это можно делать."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (обязательно)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Драйвер PawnIO (обязательно)"
; Component name.
${LangFileString} Inst_SecStartMenu "Ярлык в меню «Пуск»"
; Component name.
${LangFileString} Inst_SecDesktop "Ярлык на рабочем столе"
; Component name.
${LangFileString} Inst_SecAutostart "Запуск вместе с Windows"
; Component description.
${LangFileString} Inst_DescCore "Приложение OpenSense и его фоновая служба со всем необходимым для работы."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Подписанный драйвер, с помощью которого OpenSense считывает собственный датчик температуры процессора, как ThrottleStop и HWiNFO. Пропускается, если PawnIO уже установлен."
; Component description.
${LangFileString} Inst_DescStartMenu "Добавляет OpenSense в меню «Пуск»."
; Component description.
${LangFileString} Inst_DescDesktop "Добавляет ярлык OpenSense на рабочий стол."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Открывает OpenSense в области уведомлений при входе в систему. Управление вентиляторами в любом случае работает с момента загрузки."
; Progress log line.
${LangFileString} Inst_Closing "Закрытие запущенного OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Удаление установленной версии из $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Не удалось удалить установленную версию OpenSense (ошибка $1). Продолжить всё равно?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 уже установлен."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Установка PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Не удалось установить драйвер PawnIO (ошибка $1). До его установки OpenSense будет показывать менее точную температуру ЦП от встроенного ПО."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Регистрация службы OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Не удалось зарегистрировать службу OpenSense (ошибка $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Запуск службы OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Служба OpenSense не запустилась (ошибка $0). Её журнал находится в папке %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Управление вентиляторами, производительностью и подсветкой ноутбуков Acer Nitro и Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Управление вентиляторами, производительностью и подсветкой"
${LangFileString} Inst_Needs64Bit "Для OpenSense требуется 64-разрядная версия Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "Для OpenSense требуется Windows 10 версии 2004 (сборка ${MIN_WINDOWS_BUILD}) или более поздней."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Остановка службы OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Удалить также ваши параметры и журналы OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Некоторые файлы OpenSense используются и будут удалены после перезапуска Windows."
