; OpenSense's own installer text in Swedish, next to the app's Strings\sv-SE.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Swedish"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Starta OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Du behöver inte godkänna den här licensen för att använda OpenSense. Den ger dig rätt att dela och ändra programmet och anger villkoren för det."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (krävs)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO-drivrutin (krävs)"
; Component name.
${LangFileString} Inst_SecStartMenu "Genväg på Start-menyn"
; Component name.
${LangFileString} Inst_SecDesktop "Genväg på skrivbordet"
; Component name.
${LangFileString} Inst_SecAutostart "Starta med Windows"
; Component description.
${LangFileString} Inst_DescCore "OpenSense-appen och dess bakgrundstjänst, med allt de behöver för att köras."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Signerad drivrutin som låter OpenSense läsa CPU:ns egen temperatursensor, som ThrottleStop och HWiNFO. Hoppas över om PawnIO redan är installerat."
; Component description.
${LangFileString} Inst_DescStartMenu "Lägger till OpenSense på Start-menyn."
; Component description.
${LangFileString} Inst_DescDesktop "Lägger till en genväg till OpenSense på skrivbordet."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Öppnar OpenSense i meddelandefältet när du loggar in. Fläktstyrningen körs från start oavsett."
; Progress log line.
${LangFileString} Inst_Closing "Stänger den OpenSense som körs..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Tar bort den installerade versionen från $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Det gick inte att ta bort den installerade versionen av OpenSense (fel $1). Vill du fortsätta ändå?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 är redan installerat."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Installerar PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Det gick inte att installera PawnIO-drivrutinen (fel $1). Tills den är installerad visar OpenSense firmwarens mindre exakta CPU-temperatur."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Registrerar OpenSense-tjänsten..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Det gick inte att registrera OpenSense-tjänsten (fel $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Startar OpenSense-tjänsten..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense-tjänsten startade inte (fel $0). Loggen finns i %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Styrning av fläktar, prestanda och belysning för bärbara Acer Nitro- och Predator-datorer."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Styrning av fläktar, prestanda och belysning"
${LangFileString} Inst_Needs64Bit "OpenSense kräver 64-bitars Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense kräver Windows 10 version 2004 (version ${MIN_WINDOWS_BUILD}) eller senare."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Stoppar OpenSense-tjänsten..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Vill du även ta bort dina OpenSense-inställningar och loggar?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Vissa OpenSense-filer används och tas bort när Windows startas om."
