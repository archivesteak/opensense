; OpenSense's own installer text in TradChinese, next to the app's Strings\zh-TW.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "TradChinese"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "啟動 OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "您不需要接受此授權就能使用 OpenSense。此授權賦予您分享及修改本軟體的權利，並規定這麼做的條件。"
; Component name.
${LangFileString} Inst_SecCore "OpenSense（必要）"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO 驅動程式（必要）"
; Component name.
${LangFileString} Inst_SecStartMenu "[開始] 功能表捷徑"
; Component name.
${LangFileString} Inst_SecDesktop "桌面捷徑"
; Component name.
${LangFileString} Inst_SecAutostart "隨 Windows 啟動"
; Component description.
${LangFileString} Inst_DescCore "OpenSense 應用程式及其背景服務，以及執行所需的一切。"
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "已簽署的驅動程式，可讓 OpenSense 像 ThrottleStop 與 HWiNFO 一樣讀取 CPU 本身的溫度感應器。如果已安裝 PawnIO，則會略過。"
; Component description.
${LangFileString} Inst_DescStartMenu "將 OpenSense 新增至 [開始] 功能表。"
; Component description.
${LangFileString} Inst_DescDesktop "在桌面上新增 OpenSense 捷徑。"
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "登入時在通知區域中開啟 OpenSense。無論如何，風扇控制都會從啟動時開始執行。"
; Progress log line.
${LangFileString} Inst_Closing "正在關閉正在執行的 OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "正在從 $0 移除已安裝的版本..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "無法移除已安裝的 OpenSense 版本（錯誤 $1）。仍要繼續嗎？"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 已安裝。"
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "正在安裝 PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "無法安裝 PawnIO 驅動程式（錯誤 $1）。在安裝之前，OpenSense 會顯示韌體提供、準確度較低的 CPU 溫度。"
; Progress log line.
${LangFileString} Inst_ServiceRegistering "正在註冊 OpenSense 服務..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "無法註冊 OpenSense 服務（錯誤 $0）。"
; Progress log line.
${LangFileString} Inst_ServiceStarting "正在啟動 OpenSense 服務..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense 服務未啟動（錯誤 $0）。其記錄檔位於 %ProgramData%\OpenSense\logs。"
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "適用於 Acer Nitro 與 Predator 筆記型電腦的風扇、效能與燈光控制。"
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "風扇、效能與燈光控制"
${LangFileString} Inst_Needs64Bit "OpenSense 需要 64 位元的 Windows。"
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense 需要 Windows 10 版本 2004（組建 ${MIN_WINDOWS_BUILD}）或更新版本。"
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "正在停止 OpenSense 服務..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "是否也要移除您的 OpenSense 設定與記錄檔？"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "部分 OpenSense 檔案正在使用中，將在 Windows 重新啟動時移除。"
