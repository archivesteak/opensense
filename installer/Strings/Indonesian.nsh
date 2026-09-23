; OpenSense's own installer text in Indonesian, next to the app's Strings\id-ID.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Indonesian"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Mulai OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Anda tidak perlu menerima lisensi ini untuk menggunakan OpenSense. Lisensi ini memberi Anda hak untuk membagikan dan mengubahnya, serta menetapkan syarat untuk melakukannya."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (wajib)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Driver PawnIO (wajib)"
; Component name.
${LangFileString} Inst_SecStartMenu "Pintasan menu Mulai"
; Component name.
${LangFileString} Inst_SecDesktop "Pintasan desktop"
; Component name.
${LangFileString} Inst_SecAutostart "Mulai bersama Windows"
; Component description.
${LangFileString} Inst_DescCore "Aplikasi OpenSense dan layanan latar belakangnya, dengan semua yang diperlukan untuk berjalan."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Driver bertanda tangan yang memungkinkan OpenSense membaca sensor suhu CPU itu sendiri, seperti ThrottleStop dan HWiNFO. Dilewati jika PawnIO sudah terinstal."
; Component description.
${LangFileString} Inst_DescStartMenu "Menambahkan OpenSense ke menu Mulai."
; Component description.
${LangFileString} Inst_DescDesktop "Menambahkan pintasan OpenSense ke desktop."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Membuka OpenSense di area pemberitahuan saat Anda masuk. Kontrol kipas tetap berjalan sejak startup."
; Progress log line.
${LangFileString} Inst_Closing "Menutup OpenSense yang sedang berjalan..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Menghapus versi terinstal dari $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Versi OpenSense yang terinstal tidak dapat dihapus (kesalahan $1). Tetap lanjutkan?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 sudah terinstal."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Menginstal PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Driver PawnIO tidak dapat diinstal (kesalahan $1). Sampai terinstal, OpenSense akan menampilkan suhu CPU dari firmware yang kurang akurat."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Mendaftarkan layanan OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Layanan OpenSense tidak dapat didaftarkan (kesalahan $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Memulai layanan OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Layanan OpenSense tidak dapat dimulai (kesalahan $0). Log-nya ada di %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Kontrol kipas, performa, dan pencahayaan untuk laptop Acer Nitro dan Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Kontrol kipas, performa, dan pencahayaan"
${LangFileString} Inst_Needs64Bit "OpenSense memerlukan Windows 64-bit."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense memerlukan Windows 10 versi 2004 (build ${MIN_WINDOWS_BUILD}) atau yang lebih baru."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Menghentikan layanan OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Hapus juga pengaturan dan log OpenSense Anda?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Beberapa file OpenSense sedang digunakan dan akan dihapus saat Windows dimulai ulang."
