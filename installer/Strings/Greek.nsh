; OpenSense's own installer text in Greek, next to the app's Strings\el-GR.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Greek"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Εκκίνηση του OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Δεν χρειάζεται να αποδεχτείτε αυτή την άδεια για να χρησιμοποιήσετε το OpenSense. Σας δίνει το δικαίωμα να το διανέμετε και να το τροποποιείτε και ορίζει τους όρους για αυτό."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (απαιτείται)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Πρόγραμμα οδήγησης PawnIO (απαιτείται)"
; Component name.
${LangFileString} Inst_SecStartMenu "Συντόμευση στο μενού Έναρξη"
; Component name.
${LangFileString} Inst_SecDesktop "Συντόμευση στην επιφάνεια εργασίας"
; Component name.
${LangFileString} Inst_SecAutostart "Εκκίνηση με τα Windows"
; Component description.
${LangFileString} Inst_DescCore "Η εφαρμογή OpenSense και η υπηρεσία παρασκηνίου της, με ό,τι χρειάζονται για να λειτουργούν."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Υπογεγραμμένο πρόγραμμα οδήγησης που επιτρέπει στο OpenSense να διαβάζει τον αισθητήρα θερμοκρασίας της ίδιας της CPU, όπως το ThrottleStop και το HWiNFO. Παραλείπεται αν το PawnIO είναι ήδη εγκατεστημένο."
; Component description.
${LangFileString} Inst_DescStartMenu "Προσθέτει το OpenSense στο μενού Έναρξη."
; Component description.
${LangFileString} Inst_DescDesktop "Προσθέτει μια συντόμευση του OpenSense στην επιφάνεια εργασίας."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Ανοίγει το OpenSense στην περιοχή ειδοποιήσεων όταν συνδέεστε. Ο έλεγχος ανεμιστήρων λειτουργεί από την εκκίνηση σε κάθε περίπτωση."
; Progress log line.
${LangFileString} Inst_Closing "Κλείσιμο του OpenSense που εκτελείται..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Κατάργηση της εγκατεστημένης έκδοσης από $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Δεν ήταν δυνατή η κατάργηση της εγκατεστημένης έκδοσης του OpenSense (σφάλμα $1). Θέλετε να συνεχίσετε;"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "Το PawnIO $0 είναι ήδη εγκατεστημένο."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Εγκατάσταση του PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Δεν ήταν δυνατή η εγκατάσταση του προγράμματος οδήγησης PawnIO (σφάλμα $1). Μέχρι τότε, το OpenSense θα εμφανίζει τη λιγότερο ακριβή θερμοκρασία CPU του υλικολογισμικού."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Καταχώρηση της υπηρεσίας OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Δεν ήταν δυνατή η καταχώρηση της υπηρεσίας OpenSense (σφάλμα $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Εκκίνηση της υπηρεσίας OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Η υπηρεσία OpenSense δεν ξεκίνησε (σφάλμα $0). Το αρχείο καταγραφής της βρίσκεται στο %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Έλεγχος ανεμιστήρων, απόδοσης και φωτισμού για φορητούς Acer Nitro και Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Έλεγχος ανεμιστήρων, απόδοσης και φωτισμού"
${LangFileString} Inst_Needs64Bit "Το OpenSense απαιτεί Windows 64 bit."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "Το OpenSense απαιτεί Windows 10 έκδοση 2004 (έκδοση build ${MIN_WINDOWS_BUILD}) ή νεότερη."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Διακοπή της υπηρεσίας OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Θέλετε να καταργηθούν και οι ρυθμίσεις και τα αρχεία καταγραφής του OpenSense;"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Ορισμένα αρχεία του OpenSense χρησιμοποιούνται και θα καταργηθούν κατά την επανεκκίνηση των Windows."
