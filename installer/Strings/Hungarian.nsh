; OpenSense's own installer text in Hungarian, next to the app's Strings\hu-HU.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Hungarian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Az OpenSense indítása"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Az OpenSense használatához nem kell elfogadnia ezt a licencet. Jogot ad a program terjesztésére és módosítására, és meghatározza ennek feltételeit."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (kötelező)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO-illesztőprogram (kötelező)"
; Component name.
${LangFileString} Inst_SecStartMenu "Parancsikon a Start menüben"
; Component name.
${LangFileString} Inst_SecDesktop "Parancsikon az asztalon"
; Component name.
${LangFileString} Inst_SecAutostart "Indítás a Windows rendszerrel"
; Component description.
${LangFileString} Inst_DescCore "Az OpenSense alkalmazás és háttérszolgáltatása mindazzal, ami a futásukhoz kell."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Aláírt illesztőprogram, amellyel az OpenSense a processzor saját hőmérséklet-érzékelőjét olvassa, akárcsak a ThrottleStop és a HWiNFO. Kimarad, ha a PawnIO már telepítve van."
; Component description.
${LangFileString} Inst_DescStartMenu "Hozzáadja az OpenSense-t a Start menühöz."
; Component description.
${LangFileString} Inst_DescDesktop "OpenSense-parancsikont helyez az asztalra."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Bejelentkezéskor megnyitja az OpenSense-t az értesítési területen. A ventilátorvezérlés mindenképpen fut a rendszerindítástól."
; Progress log line.
${LangFileString} Inst_Closing "A futó OpenSense bezárása..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "A telepített verzió eltávolítása innen: $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Az OpenSense telepített verziója nem távolítható el ($1. hiba). Folytatja mégis?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "A PawnIO $0 már telepítve van."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "A PawnIO telepítése..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "A PawnIO-illesztőprogram nem telepíthető ($1. hiba). Addig az OpenSense a belső vezérlőprogram kevésbé pontos processzorhőmérsékletét mutatja."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Az OpenSense szolgáltatás regisztrálása..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Az OpenSense szolgáltatás nem regisztrálható ($0. hiba)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Az OpenSense szolgáltatás indítása..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Az OpenSense szolgáltatás nem indult el ($0. hiba). Naplója itt található: %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Ventilátor-, teljesítmény- és világításvezérlés Acer Nitro és Predator laptopokhoz."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Ventilátor-, teljesítmény- és világításvezérlés"
${LangFileString} Inst_Needs64Bit "Az OpenSense 64 bites Windowst igényel."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "Az OpenSense-hez Windows 10 2004-es vagy újabb verzió szükséges (${MIN_WINDOWS_BUILD}. build)."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Az OpenSense szolgáltatás leállítása..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Az OpenSense beállításait és naplóit is eltávolítja?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Néhány OpenSense-fájl használatban van, ezért a Windows újraindításakor törlődik."
