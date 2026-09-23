; OpenSense's own installer text in Catalan, next to the app's Strings\ca-ES.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Catalan"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Inicia l'OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "No cal que acceptis aquesta llicència per fer servir l'OpenSense. Et dona el dret de compartir-lo i modificar-lo, i estableix les condicions per fer-ho."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (obligatori)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Controlador PawnIO (obligatori)"
; Component name.
${LangFileString} Inst_SecStartMenu "Drecera al menú Inici"
; Component name.
${LangFileString} Inst_SecDesktop "Drecera a l'escriptori"
; Component name.
${LangFileString} Inst_SecAutostart "Inicia amb el Windows"
; Component description.
${LangFileString} Inst_DescCore "L'aplicació OpenSense i el seu servei en segon pla, amb tot el que necessiten per funcionar."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Controlador signat que permet a l'OpenSense llegir el sensor de temperatura de la mateixa CPU, com fan ThrottleStop i HWiNFO. S'omet si el PawnIO ja està instal·lat."
; Component description.
${LangFileString} Inst_DescStartMenu "Afegeix l'OpenSense al menú Inici."
; Component description.
${LangFileString} Inst_DescDesktop "Afegeix una drecera a l'OpenSense a l'escriptori."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Obre l'OpenSense a l'àrea de notificació quan inicies la sessió. El control dels ventiladors funciona des de l'inici igualment."
; Progress log line.
${LangFileString} Inst_Closing "S'està tancant l'OpenSense en execució..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "S'està suprimint la versió instal·lada de $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "No s'ha pogut suprimir la versió instal·lada de l'OpenSense (error $1). Vols continuar igualment?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "El PawnIO $0 ja està instal·lat."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "S'està instal·lant el PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "No s'ha pogut instal·lar el controlador PawnIO (error $1). Fins que no s'instal·li, l'OpenSense mostrarà la temperatura de la CPU del microprogramari, menys precisa."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "S'està registrant el servei de l'OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "No s'ha pogut registrar el servei de l'OpenSense (error $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "S'està iniciant el servei de l'OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "El servei de l'OpenSense no s'ha iniciat (error $0). El seu registre és a %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Control dels ventiladors, el rendiment i la il·luminació per a portàtils Acer Nitro i Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Control dels ventiladors, el rendiment i la il·luminació"
${LangFileString} Inst_Needs64Bit "L'OpenSense requereix el Windows de 64 bits."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "L'OpenSense requereix el Windows 10 versió 2004 (compilació ${MIN_WINDOWS_BUILD}) o posterior."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "S'està aturant el servei de l'OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Vols suprimir també la configuració i els registres de l'OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Alguns fitxers de l'OpenSense s'estan fent servir i se suprimiran quan es reiniciï el Windows."
