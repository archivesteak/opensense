; OpenSense's own installer text in Finnish, next to the app's Strings\fi-FI.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Finnish"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Käynnistä OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Sinun ei tarvitse hyväksyä tätä käyttöoikeutta käyttääksesi OpenSenseä. Se antaa sinulle oikeuden jakaa ja muuttaa ohjelmaa ja määrittää ehdot sille."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (pakollinen)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO-ohjain (pakollinen)"
; Component name.
${LangFileString} Inst_SecStartMenu "Pikakuvake Käynnistä-valikkoon"
; Component name.
${LangFileString} Inst_SecDesktop "Pikakuvake työpöydälle"
; Component name.
${LangFileString} Inst_SecAutostart "Käynnistä Windowsin mukana"
; Component description.
${LangFileString} Inst_DescCore "OpenSense-sovellus ja sen taustapalvelu sekä kaikki, mitä ne tarvitsevat toimiakseen."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Allekirjoitettu ohjain, jonka avulla OpenSense lukee suorittimen omaa lämpötila-anturia kuten ThrottleStop ja HWiNFO. Ohitetaan, jos PawnIO on jo asennettu."
; Component description.
${LangFileString} Inst_DescStartMenu "Lisää OpenSensen Käynnistä-valikkoon."
; Component description.
${LangFileString} Inst_DescDesktop "Lisää OpenSensen pikakuvakkeen työpöydälle."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Avaa OpenSensen ilmaisinalueelle, kun kirjaudut sisään. Tuuletinohjaus toimii käynnistyksestä lähtien joka tapauksessa."
; Progress log line.
${LangFileString} Inst_Closing "Suljetaan käynnissä oleva OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Poistetaan asennettu versio kansiosta $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "OpenSensen asennetun version poistaminen epäonnistui (virhe $1). Jatketaanko silti?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 on jo asennettu."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Asennetaan PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "PawnIO-ohjaimen asentaminen epäonnistui (virhe $1). Siihen asti OpenSense näyttää laiteohjelmiston epätarkemman suoritinlämpötilan."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Rekisteröidään OpenSense-palvelu..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "OpenSense-palvelun rekisteröiminen epäonnistui (virhe $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Käynnistetään OpenSense-palvelu..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense-palvelu ei käynnistynyt (virhe $0). Sen loki on kansiossa %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Tuuletin-, suorituskyky- ja valaistusohjaus Acer Nitro- ja Predator-kannettaville."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Tuuletin-, suorituskyky- ja valaistusohjaus"
${LangFileString} Inst_Needs64Bit "OpenSense edellyttää 64-bittistä Windowsia."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense edellyttää Windows 10 -versiota 2004 (koontiversio ${MIN_WINDOWS_BUILD}) tai uudempaa."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Pysäytetään OpenSense-palvelu..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Poistetaanko myös OpenSensen asetukset ja lokit?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Jotkin OpenSensen tiedostot ovat käytössä, ja ne poistetaan, kun Windows käynnistetään uudelleen."
