; OpenSense's own installer text in Spanish, next to the app's Strings\es-ES.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Spanish"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Iniciar OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "No necesitas aceptar esta licencia para usar OpenSense. Te da derecho a compartirlo y modificarlo, y establece las condiciones para hacerlo."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (necesario)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Controlador PawnIO (necesario)"
; Component name.
${LangFileString} Inst_SecStartMenu "Acceso directo en el menú Inicio"
; Component name.
${LangFileString} Inst_SecDesktop "Acceso directo en el escritorio"
; Component name.
${LangFileString} Inst_SecAutostart "Iniciar con Windows"
; Component description.
${LangFileString} Inst_DescCore "La aplicación OpenSense y su servicio en segundo plano, con todo lo que necesitan para funcionar."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Controlador firmado que permite a OpenSense leer el propio sensor de temperatura de la CPU, como ThrottleStop y HWiNFO. Se omite si PawnIO ya está instalado."
; Component description.
${LangFileString} Inst_DescStartMenu "Añade OpenSense al menú Inicio."
; Component description.
${LangFileString} Inst_DescDesktop "Añade un acceso directo a OpenSense en el escritorio."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Abre OpenSense en el área de notificación al iniciar sesión. El control de los ventiladores funciona desde el inicio en cualquier caso."
; Progress log line.
${LangFileString} Inst_Closing "Cerrando la copia de OpenSense en ejecución..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Quitando la versión instalada de $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "No se pudo quitar la versión instalada de OpenSense (error $1). ¿Quieres continuar de todos modos?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 ya está instalado."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Instalando PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "No se pudo instalar el controlador PawnIO (error $1). Hasta que se instale, OpenSense mostrará la temperatura de la CPU del firmware, que es menos precisa."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Registrando el servicio OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "No se pudo registrar el servicio OpenSense (error $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Iniciando el servicio OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "El servicio OpenSense no se inició (error $0). Su registro está en %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Control de ventiladores, rendimiento e iluminación para portátiles Acer Nitro y Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Control de ventiladores, rendimiento e iluminación"
${LangFileString} Inst_Needs64Bit "OpenSense requiere Windows de 64 bits."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense requiere Windows 10 versión 2004 (compilación ${MIN_WINDOWS_BUILD}) o posterior."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Deteniendo el servicio OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "¿Quitar también tu configuración y tus registros de OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Algunos archivos de OpenSense están en uso y se quitarán cuando Windows se reinicie."
