; OpenSense's own installer text in Turkish, next to the app's Strings\tr-TR.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Turkish"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "OpenSense'i başlat"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "OpenSense'i kullanmak için bu lisansı kabul etmeniz gerekmez. Lisans size programı paylaşma ve değiştirme hakkı verir ve bunun koşullarını belirler."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (gerekli)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO sürücüsü (gerekli)"
; Component name.
${LangFileString} Inst_SecStartMenu "Başlat menüsü kısayolu"
; Component name.
${LangFileString} Inst_SecDesktop "Masaüstü kısayolu"
; Component name.
${LangFileString} Inst_SecAutostart "Windows ile başlat"
; Component description.
${LangFileString} Inst_DescCore "OpenSense uygulaması ve arka plan hizmeti, çalışmaları için gereken her şeyle birlikte."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "ThrottleStop ve HWiNFO gibi, OpenSense'in CPU'nun kendi sıcaklık sensörünü okumasını sağlayan imzalı sürücü. PawnIO zaten yüklüyse atlanır."
; Component description.
${LangFileString} Inst_DescStartMenu "OpenSense'i Başlat menüsüne ekler."
; Component description.
${LangFileString} Inst_DescDesktop "Masaüstüne bir OpenSense kısayolu ekler."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Oturum açtığınızda OpenSense'i bildirim alanında açar. Fan denetimi her durumda başlangıçtan itibaren çalışır."
; Progress log line.
${LangFileString} Inst_Closing "Çalışan OpenSense kapatılıyor..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Yüklü sürüm $0 konumundan kaldırılıyor..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "OpenSense'in yüklü sürümü kaldırılamadı (hata $1). Yine de devam edilsin mi?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 zaten yüklü."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "PawnIO yükleniyor..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "PawnIO sürücüsü yüklenemedi (hata $1). Yüklenene kadar OpenSense, üretici yazılımının daha az doğru CPU sıcaklığını gösterir."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "OpenSense hizmeti kaydediliyor..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "OpenSense hizmeti kaydedilemedi (hata $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "OpenSense hizmeti başlatılıyor..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense hizmeti başlatılamadı (hata $0). Günlüğü %ProgramData%\OpenSense\logs konumunda."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Acer Nitro ve Predator dizüstü bilgisayarlar için fan, performans ve aydınlatma denetimi."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Fan, performans ve aydınlatma denetimi"
${LangFileString} Inst_Needs64Bit "OpenSense için 64 bit Windows gerekir."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense için Windows 10 sürüm 2004 (derleme ${MIN_WINDOWS_BUILD}) veya üzeri gerekir."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "OpenSense hizmeti durduruluyor..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "OpenSense ayarlarınız ve günlükleriniz de kaldırılsın mı?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Bazı OpenSense dosyaları kullanımda ve Windows yeniden başlatıldığında kaldırılacak."
