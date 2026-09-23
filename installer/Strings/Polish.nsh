; OpenSense's own installer text in Polish, next to the app's Strings\pl-PL.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Polish"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Uruchom OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Nie musisz akceptować tej licencji, aby używać OpenSense. Daje Ci ona prawo do rozpowszechniania i modyfikowania programu oraz określa warunki, na jakich możesz to robić."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (wymagany)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Sterownik PawnIO (wymagany)"
; Component name.
${LangFileString} Inst_SecStartMenu "Skrót w menu Start"
; Component name.
${LangFileString} Inst_SecDesktop "Skrót na pulpicie"
; Component name.
${LangFileString} Inst_SecAutostart "Uruchamiaj z systemem Windows"
; Component description.
${LangFileString} Inst_DescCore "Aplikacja OpenSense i jej usługa działająca w tle wraz ze wszystkim, czego potrzebują do działania."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Podpisany sterownik, dzięki któremu OpenSense odczytuje wbudowany czujnik temperatury procesora, tak jak ThrottleStop i HWiNFO. Pomijany, jeśli PawnIO jest już zainstalowany."
; Component description.
${LangFileString} Inst_DescStartMenu "Dodaje OpenSense do menu Start."
; Component description.
${LangFileString} Inst_DescDesktop "Dodaje skrót do OpenSense na pulpicie."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Otwiera OpenSense w obszarze powiadomień po zalogowaniu. Sterowanie wentylatorami i tak działa od uruchomienia."
; Progress log line.
${LangFileString} Inst_Closing "Zamykanie działającego OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Usuwanie zainstalowanej wersji z folderu $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Nie można usunąć zainstalowanej wersji OpenSense (błąd $1). Czy mimo to kontynuować?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 jest już zainstalowany."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Instalowanie PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Nie można zainstalować sterownika PawnIO (błąd $1). Do tego czasu OpenSense będzie pokazywać mniej dokładną temperaturę procesora z oprogramowania układowego."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Rejestrowanie usługi OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Nie można zarejestrować usługi OpenSense (błąd $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Uruchamianie usługi OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Usługa OpenSense nie została uruchomiona (błąd $0). Jej dziennik znajduje się w folderze %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Sterowanie wentylatorami, wydajnością i podświetleniem laptopów Acer Nitro i Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Sterowanie wentylatorami, wydajnością i podświetleniem"
${LangFileString} Inst_Needs64Bit "OpenSense wymaga 64-bitowego systemu Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense wymaga systemu Windows 10 w wersji 2004 (kompilacja ${MIN_WINDOWS_BUILD}) lub nowszej."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Zatrzymywanie usługi OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Usunąć również Twoje ustawienia i dzienniki OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Niektóre pliki OpenSense są używane i zostaną usunięte po ponownym uruchomieniu systemu Windows."
