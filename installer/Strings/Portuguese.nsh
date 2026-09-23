; OpenSense's own installer text in Portuguese, next to the app's Strings\pt-PT.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Portuguese"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Iniciar o OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Não precisa de aceitar esta licença para utilizar o OpenSense. Dá-lhe o direito de o partilhar e modificar e define as condições para o fazer."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (obrigatório)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Controlador PawnIO (obrigatório)"
; Component name.
${LangFileString} Inst_SecStartMenu "Atalho no menu Iniciar"
; Component name.
${LangFileString} Inst_SecDesktop "Atalho no ambiente de trabalho"
; Component name.
${LangFileString} Inst_SecAutostart "Iniciar com o Windows"
; Component description.
${LangFileString} Inst_DescCore "A aplicação OpenSense e o respetivo serviço em segundo plano, com tudo o que precisam para funcionar."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Controlador assinado que permite ao OpenSense ler o sensor de temperatura da própria CPU, tal como o ThrottleStop e o HWiNFO. É ignorado se o PawnIO já estiver instalado."
; Component description.
${LangFileString} Inst_DescStartMenu "Adiciona o OpenSense ao menu Iniciar."
; Component description.
${LangFileString} Inst_DescDesktop "Adiciona um atalho do OpenSense ao ambiente de trabalho."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Abre o OpenSense na área de notificação quando inicia sessão. O controlo das ventoinhas funciona desde o arranque em qualquer caso."
; Progress log line.
${LangFileString} Inst_Closing "A fechar o OpenSense em execução..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "A remover a versão instalada de $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Não foi possível remover a versão instalada do OpenSense (erro $1). Pretende continuar mesmo assim?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "O PawnIO $0 já está instalado."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "A instalar o PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Não foi possível instalar o controlador PawnIO (erro $1). Até que seja instalado, o OpenSense mostra a temperatura da CPU do firmware, menos precisa."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "A registar o serviço do OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Não foi possível registar o serviço do OpenSense (erro $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "A iniciar o serviço do OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "O serviço do OpenSense não foi iniciado (erro $0). O registo encontra-se em %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Controlo das ventoinhas, do desempenho e da iluminação para portáteis Acer Nitro e Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Controlo das ventoinhas, do desempenho e da iluminação"
${LangFileString} Inst_Needs64Bit "O OpenSense requer o Windows de 64 bits."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "O OpenSense requer o Windows 10 versão 2004 (compilação ${MIN_WINDOWS_BUILD}) ou posterior."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "A parar o serviço do OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Remover também as suas definições e registos do OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Alguns ficheiros do OpenSense estão a ser utilizados e serão removidos quando o Windows reiniciar."
