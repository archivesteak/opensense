; OpenSense's own installer text in French, next to the app's Strings\fr-FR.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "French"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Démarrer OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Vous n’avez pas besoin d’accepter cette licence pour utiliser OpenSense. Elle vous donne le droit de le partager et de le modifier, et fixe les conditions pour le faire."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (requis)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Pilote PawnIO (requis)"
; Component name.
${LangFileString} Inst_SecStartMenu "Raccourci dans le menu Démarrer"
; Component name.
${LangFileString} Inst_SecDesktop "Raccourci sur le Bureau"
; Component name.
${LangFileString} Inst_SecAutostart "Démarrer avec Windows"
; Component description.
${LangFileString} Inst_DescCore "L’application OpenSense et son service en arrière-plan, avec tout ce dont ils ont besoin pour fonctionner."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Pilote signé qui permet à OpenSense de lire le capteur de température propre au processeur, comme ThrottleStop et HWiNFO. Ignoré si PawnIO est déjà installé."
; Component description.
${LangFileString} Inst_DescStartMenu "Ajoute OpenSense au menu Démarrer."
; Component description.
${LangFileString} Inst_DescDesktop "Ajoute un raccourci vers OpenSense sur le Bureau."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Ouvre OpenSense dans la zone de notification lorsque vous vous connectez. Le contrôle des ventilateurs fonctionne dès le démarrage dans tous les cas."
; Progress log line.
${LangFileString} Inst_Closing "Fermeture de l’instance d’OpenSense en cours d’exécution..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Suppression de la version installée dans $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Impossible de supprimer la version installée d’OpenSense (erreur $1). Continuer quand même ?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 est déjà installé."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Installation de PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Impossible d’installer le pilote PawnIO (erreur $1). En attendant, OpenSense affichera la température du processeur fournie par le firmware, moins précise."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Inscription du service OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Impossible d’inscrire le service OpenSense (erreur $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Démarrage du service OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Le service OpenSense n’a pas démarré (erreur $0). Son journal se trouve dans %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Contrôle des ventilateurs, des performances et de l’éclairage pour les ordinateurs portables Acer Nitro et Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Contrôle des ventilateurs, des performances et de l’éclairage"
${LangFileString} Inst_Needs64Bit "OpenSense nécessite une version 64 bits de Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense nécessite Windows 10 version 2004 (build ${MIN_WINDOWS_BUILD}) ou ultérieure."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Arrêt du service OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Supprimer aussi vos paramètres et journaux OpenSense ?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Certains fichiers d’OpenSense sont en cours d’utilisation et seront supprimés au redémarrage de Windows."
