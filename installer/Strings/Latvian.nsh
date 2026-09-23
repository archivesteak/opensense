; OpenSense's own installer text in Latvian, next to the app's Strings\lv-LV.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Latvian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Startēt OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Lai izmantotu OpenSense, šī licence jums nav jāakceptē. Tā piešķir jums tiesības to kopīgot un mainīt un nosaka nosacījumus, kā to darīt."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (obligāts)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO draiveris (obligāts)"
; Component name.
${LangFileString} Inst_SecStartMenu "Saīsne izvēlnē Sākt"
; Component name.
${LangFileString} Inst_SecDesktop "Saīsne darbvirsmā"
; Component name.
${LangFileString} Inst_SecAutostart "Startēt kopā ar Windows"
; Component description.
${LangFileString} Inst_DescCore "OpenSense lietotne un tās fona pakalpojums ar visu, kas nepieciešams to darbībai."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Parakstīts draiveris, kas ļauj OpenSense nolasīt paša procesora temperatūras sensoru tāpat kā ThrottleStop un HWiNFO. Tiek izlaists, ja PawnIO jau ir instalēts."
; Component description.
${LangFileString} Inst_DescStartMenu "Pievieno OpenSense izvēlnei Sākt."
; Component description.
${LangFileString} Inst_DescDesktop "Pievieno OpenSense saīsni darbvirsmai."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Pierakstoties atver OpenSense paziņojumu apgabalā. Ventilatoru vadība jebkurā gadījumā darbojas no startēšanas."
; Progress log line.
${LangFileString} Inst_Closing "Notiek darbojošā OpenSense aizvēršana..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Notiek instalētās versijas noņemšana no $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Neizdevās noņemt instalēto OpenSense versiju (kļūda $1). Vai tomēr turpināt?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 jau ir instalēts."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Notiek PawnIO instalēšana..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Neizdevās instalēt PawnIO draiveri (kļūda $1). Līdz tam OpenSense rādīs mazāk precīzo aparātprogrammatūras procesora temperatūru."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Notiek OpenSense pakalpojuma reģistrēšana..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Neizdevās reģistrēt OpenSense pakalpojumu (kļūda $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Notiek OpenSense pakalpojuma startēšana..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense pakalpojums netika startēts (kļūda $0). Tā žurnāls atrodas mapē %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Ventilatoru, veiktspējas un apgaismojuma vadība Acer Nitro un Predator klēpjdatoriem."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Ventilatoru, veiktspējas un apgaismojuma vadība"
${LangFileString} Inst_Needs64Bit "OpenSense nepieciešama 64 bitu Windows versija."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense nepieciešama Windows 10 versija 2004 (būvējums ${MIN_WINDOWS_BUILD}) vai jaunāka."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Notiek OpenSense pakalpojuma apturēšana..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Vai noņemt arī jūsu OpenSense iestatījumus un žurnālus?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Daži OpenSense faili tiek izmantoti, un tie tiks noņemti, kad Windows tiks restartēta."
