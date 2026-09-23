; OpenSense's own installer text in Czech, next to the app's Strings\cs-CZ.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Czech"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Spustit OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "K používání OpenSense nemusíte tuto licenci přijmout. Dává vám právo program šířit a upravovat a stanovuje podmínky, za kterých to můžete dělat."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (povinné)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Ovladač PawnIO (povinné)"
; Component name.
${LangFileString} Inst_SecStartMenu "Zástupce v nabídce Start"
; Component name.
${LangFileString} Inst_SecDesktop "Zástupce na ploše"
; Component name.
${LangFileString} Inst_SecAutostart "Spouštět se systémem Windows"
; Component description.
${LangFileString} Inst_DescCore "Aplikace OpenSense a její služba na pozadí se vším, co potřebují ke spuštění."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Podepsaný ovladač, díky kterému OpenSense čte vlastní teplotní senzor procesoru stejně jako ThrottleStop a HWiNFO. Pokud je PawnIO už nainstalovaný, přeskočí se."
; Component description.
${LangFileString} Inst_DescStartMenu "Přidá OpenSense do nabídky Start."
; Component description.
${LangFileString} Inst_DescDesktop "Přidá zástupce OpenSense na plochu."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Po přihlášení otevře OpenSense v oznamovací oblasti. Ovládání ventilátorů běží od spuštění v každém případě."
; Progress log line.
${LangFileString} Inst_Closing "Ukončování spuštěného OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Odebírání nainstalované verze ze složky $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Nainstalovanou verzi OpenSense se nepodařilo odebrat (chyba $1). Chcete přesto pokračovat?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 už je nainstalovaný."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Instalace PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Ovladač PawnIO se nepodařilo nainstalovat (chyba $1). Do té doby bude OpenSense zobrazovat méně přesnou teplotu procesoru z firmwaru."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Registrace služby OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Službu OpenSense se nepodařilo zaregistrovat (chyba $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Spouštění služby OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Služba OpenSense se nespustila (chyba $0). Její protokol je ve složce %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Ovládání ventilátorů, výkonu a podsvícení pro notebooky Acer Nitro a Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Ovládání ventilátorů, výkonu a podsvícení"
${LangFileString} Inst_Needs64Bit "OpenSense vyžaduje 64bitový systém Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense vyžaduje Windows 10 verze 2004 (build ${MIN_WINDOWS_BUILD}) nebo novější."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Zastavování služby OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Odebrat také nastavení a protokoly OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Některé soubory OpenSense se používají a budou odebrány po restartování Windows."
