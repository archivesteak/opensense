; OpenSense's own installer text in Italian, next to the app's Strings\it-IT.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Italian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Avvia OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Non è necessario accettare questa licenza per usare OpenSense. Ti concede il diritto di condividerlo e modificarlo e stabilisce le condizioni per farlo."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (obbligatorio)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Driver PawnIO (obbligatorio)"
; Component name.
${LangFileString} Inst_SecStartMenu "Collegamento nel menu Start"
; Component name.
${LangFileString} Inst_SecDesktop "Collegamento sul desktop"
; Component name.
${LangFileString} Inst_SecAutostart "Avvia con Windows"
; Component description.
${LangFileString} Inst_DescCore "L'app OpenSense e il suo servizio in background, con tutto ciò che serve per funzionare."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Driver firmato che consente a OpenSense di leggere il sensore di temperatura della CPU, come ThrottleStop e HWiNFO. Viene saltato se PawnIO è già installato."
; Component description.
${LangFileString} Inst_DescStartMenu "Aggiunge OpenSense al menu Start."
; Component description.
${LangFileString} Inst_DescDesktop "Aggiunge un collegamento a OpenSense sul desktop."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Apre OpenSense nell'area di notifica quando accedi. Il controllo delle ventole è comunque attivo dall'avvio."
; Progress log line.
${LangFileString} Inst_Closing "Chiusura della copia di OpenSense in esecuzione..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Rimozione della versione installata da $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Impossibile rimuovere la versione installata di OpenSense (errore $1). Continuare comunque?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 è già installato."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Installazione di PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Impossibile installare il driver PawnIO (errore $1). Fino ad allora OpenSense mostrerà la temperatura della CPU fornita dal firmware, meno precisa."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Registrazione del servizio OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Impossibile registrare il servizio OpenSense (errore $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Avvio del servizio OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Il servizio OpenSense non è stato avviato (errore $0). Il suo log si trova in %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Controllo di ventole, prestazioni e illuminazione per i portatili Acer Nitro e Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Controllo di ventole, prestazioni e illuminazione"
${LangFileString} Inst_Needs64Bit "OpenSense richiede Windows a 64 bit."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense richiede Windows 10 versione 2004 (build ${MIN_WINDOWS_BUILD}) o successiva."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Arresto del servizio OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Rimuovere anche le impostazioni e i log di OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Alcuni file di OpenSense sono in uso e verranno rimossi al riavvio di Windows."
