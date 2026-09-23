; OpenSense's own installer text in Dutch, next to the app's Strings\nl-NL.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Dutch"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "OpenSense starten"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Je hoeft deze licentie niet te accepteren om OpenSense te gebruiken. De licentie geeft je het recht om het programma te delen en te wijzigen, en legt de voorwaarden daarvoor vast."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (vereist)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO-stuurprogramma (vereist)"
; Component name.
${LangFileString} Inst_SecStartMenu "Snelkoppeling in menu Start"
; Component name.
${LangFileString} Inst_SecDesktop "Snelkoppeling op bureaublad"
; Component name.
${LangFileString} Inst_SecAutostart "Starten met Windows"
; Component description.
${LangFileString} Inst_DescCore "De OpenSense-app en de bijbehorende achtergrondservice, met alles wat ze nodig hebben."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Ondertekend stuurprogramma waarmee OpenSense de eigen temperatuursensor van de CPU uitleest, zoals ThrottleStop en HWiNFO. Wordt overgeslagen als PawnIO al is geïnstalleerd."
; Component description.
${LangFileString} Inst_DescStartMenu "Voegt OpenSense toe aan het menu Start."
; Component description.
${LangFileString} Inst_DescDesktop "Voegt een snelkoppeling naar OpenSense toe aan het bureaublad."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Opent OpenSense in het systeemvak wanneer je je aanmeldt. De ventilatorregeling werkt hoe dan ook vanaf het opstarten."
; Progress log line.
${LangFileString} Inst_Closing "De actieve OpenSense sluiten..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "De geïnstalleerde versie verwijderen uit $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "De geïnstalleerde versie van OpenSense kan niet worden verwijderd (fout $1). Toch doorgaan?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 is al geïnstalleerd."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "PawnIO installeren..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Het PawnIO-stuurprogramma kan niet worden geïnstalleerd (fout $1). Tot die tijd toont OpenSense de minder nauwkeurige CPU-temperatuur van de firmware."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "De OpenSense-service registreren..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "De OpenSense-service kan niet worden geregistreerd (fout $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "De OpenSense-service starten..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "De OpenSense-service is niet gestart (fout $0). Het logboek staat in %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Regeling van ventilatoren, prestaties en verlichting voor Acer Nitro- en Predator-laptops."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Regeling van ventilatoren, prestaties en verlichting"
${LangFileString} Inst_Needs64Bit "Voor OpenSense is een 64-bits versie van Windows vereist."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "Voor OpenSense is Windows 10 versie 2004 (build ${MIN_WINDOWS_BUILD}) of hoger vereist."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "De OpenSense-service stoppen..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Ook je OpenSense-instellingen en -logboeken verwijderen?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Sommige OpenSense-bestanden zijn in gebruik en worden verwijderd wanneer Windows opnieuw wordt opgestart."
