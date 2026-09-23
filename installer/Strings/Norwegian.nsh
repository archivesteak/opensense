; OpenSense's own installer text in Norwegian, next to the app's Strings\nb-NO.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Norwegian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Start OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Du trenger ikke å godta denne lisensen for å bruke OpenSense. Den gir deg rett til å dele og endre programmet og fastsetter vilkårene for å gjøre det."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (påkrevd)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO-driver (påkrevd)"
; Component name.
${LangFileString} Inst_SecStartMenu "Snarvei på Start-menyen"
; Component name.
${LangFileString} Inst_SecDesktop "Snarvei på skrivebordet"
; Component name.
${LangFileString} Inst_SecAutostart "Start med Windows"
; Component description.
${LangFileString} Inst_DescCore "OpenSense-appen og bakgrunnstjenesten dens, med alt de trenger for å kjøre."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Signert driver som lar OpenSense lese CPU-ens egen temperatursensor, slik ThrottleStop og HWiNFO gjør. Hoppes over hvis PawnIO allerede er installert."
; Component description.
${LangFileString} Inst_DescStartMenu "Legger til OpenSense på Start-menyen."
; Component description.
${LangFileString} Inst_DescDesktop "Legger til en snarvei til OpenSense på skrivebordet."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Åpner OpenSense i systemstatusfeltet når du logger på. Viftestyringen kjører fra oppstart uansett."
; Progress log line.
${LangFileString} Inst_Closing "Lukker OpenSense-kopien som kjører..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Fjerner den installerte versjonen fra $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Kan ikke fjerne den installerte versjonen av OpenSense (feil $1). Vil du fortsette likevel?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 er allerede installert."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Installerer PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Kan ikke installere PawnIO-driveren (feil $1). Inntil den er installert, viser OpenSense fastvarens mindre nøyaktige CPU-temperatur."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Registrerer OpenSense-tjenesten..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Kan ikke registrere OpenSense-tjenesten (feil $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Starter OpenSense-tjenesten..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense-tjenesten startet ikke (feil $0). Loggen ligger i %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Styring av vifter, ytelse og belysning for bærbare Acer Nitro- og Predator-PC-er."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Styring av vifter, ytelse og belysning"
${LangFileString} Inst_Needs64Bit "OpenSense krever 64-biters Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense krever Windows 10 versjon 2004 (build ${MIN_WINDOWS_BUILD}) eller nyere."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Stopper OpenSense-tjenesten..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Vil du også fjerne OpenSense-innstillingene og -loggene dine?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Noen OpenSense-filer er i bruk og fjernes når Windows startes på nytt."
