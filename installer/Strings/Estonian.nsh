; OpenSense's own installer text in Estonian, next to the app's Strings\et-EE.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Estonian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Käivita OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "OpenSense'i kasutamiseks ei pea te seda litsentsi aktsepteerima. See annab teile õiguse programmi jagada ja muuta ning määrab selle tingimused."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (nõutav)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO draiver (nõutav)"
; Component name.
${LangFileString} Inst_SecStartMenu "Otsetee menüüs Start"
; Component name.
${LangFileString} Inst_SecDesktop "Otsetee töölaual"
; Component name.
${LangFileString} Inst_SecAutostart "Käivita koos Windowsiga"
; Component description.
${LangFileString} Inst_DescCore "OpenSense'i rakendus ja selle taustateenus koos kõigega, mida need töötamiseks vajavad."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Allkirjastatud draiver, mis võimaldab OpenSense'il lugeda protsessori enda temperatuuriandurit, nagu ThrottleStop ja HWiNFO. Jäetakse vahele, kui PawnIO on juba installitud."
; Component description.
${LangFileString} Inst_DescStartMenu "Lisab OpenSense'i menüüsse Start."
; Component description.
${LangFileString} Inst_DescDesktop "Lisab töölauale OpenSense'i otsetee."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Avab sisselogimisel OpenSense'i teavitusalas. Ventilaatorite juhtimine töötab käivitamisest alates igal juhul."
; Progress log line.
${LangFileString} Inst_Closing "Töötava OpenSense'i sulgemine..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Installitud versiooni eemaldamine kaustast $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "OpenSense'i installitud versiooni ei saanud eemaldada (tõrge $1). Kas jätkata ikkagi?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 on juba installitud."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "PawnIO installimine..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "PawnIO draiverit ei saanud installida (tõrge $1). Seni kuvab OpenSense püsivara vähem täpset protsessori temperatuuri."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "OpenSense'i teenuse registreerimine..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "OpenSense'i teenust ei saanud registreerida (tõrge $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "OpenSense'i teenuse käivitamine..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense'i teenus ei käivitunud (tõrge $0). Selle logi on kaustas %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Ventilaatorite, jõudluse ja valgustuse juhtimine Acer Nitro ja Predatori sülearvutitele."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Ventilaatorite, jõudluse ja valgustuse juhtimine"
${LangFileString} Inst_Needs64Bit "OpenSense nõuab 64-bitist Windowsi."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense nõuab operatsioonisüsteemi Windows 10 versiooni 2004 (järk ${MIN_WINDOWS_BUILD}) või uuemat."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "OpenSense'i teenuse peatamine..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Kas eemaldada ka teie OpenSense'i sätted ja logid?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Mõned OpenSense'i failid on kasutusel ja need eemaldatakse Windowsi taaskäivitamisel."
