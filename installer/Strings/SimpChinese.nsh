; OpenSense's own installer text in SimpChinese, next to the app's Strings\zh-CN.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "SimpChinese"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "启动 OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "你无需接受此许可证即可使用 OpenSense。它赋予你分享和修改本软件的权利，并规定了这样做的条件。"
; Component name.
${LangFileString} Inst_SecCore "OpenSense（必需）"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO 驱动程序（必需）"
; Component name.
${LangFileString} Inst_SecStartMenu "“开始”菜单快捷方式"
; Component name.
${LangFileString} Inst_SecDesktop "桌面快捷方式"
; Component name.
${LangFileString} Inst_SecAutostart "随 Windows 启动"
; Component description.
${LangFileString} Inst_DescCore "OpenSense 应用及其后台服务，以及它们运行所需的一切。"
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "已签名的驱动程序，让 OpenSense 能像 ThrottleStop 和 HWiNFO 一样读取 CPU 自身的温度传感器。如果已安装 PawnIO，则跳过。"
; Component description.
${LangFileString} Inst_DescStartMenu "将 OpenSense 添加到“开始”菜单。"
; Component description.
${LangFileString} Inst_DescDesktop "在桌面上添加 OpenSense 快捷方式。"
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "登录时在通知区域中打开 OpenSense。无论如何，风扇控制都会从启动时开始运行。"
; Progress log line.
${LangFileString} Inst_Closing "正在关闭正在运行的 OpenSense..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "正在从 $0 删除已安装的版本..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "无法删除已安装的 OpenSense 版本（错误 $1）。是否仍要继续？"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 已安装。"
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "正在安装 PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "无法安装 PawnIO 驱动程序（错误 $1）。在安装之前，OpenSense 将显示固件提供的精度较低的 CPU 温度。"
; Progress log line.
${LangFileString} Inst_ServiceRegistering "正在注册 OpenSense 服务..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "无法注册 OpenSense 服务（错误 $0）。"
; Progress log line.
${LangFileString} Inst_ServiceStarting "正在启动 OpenSense 服务..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense 服务未启动（错误 $0）。其日志位于 %ProgramData%\OpenSense\logs。"
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "适用于 Acer Nitro 和 Predator 笔记本电脑的风扇、性能和灯光控制。"
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "风扇、性能和灯光控制"
${LangFileString} Inst_Needs64Bit "OpenSense 需要 64 位 Windows。"
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense 需要 Windows 10 版本 2004（内部版本 ${MIN_WINDOWS_BUILD}）或更高版本。"
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "正在停止 OpenSense 服务..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "是否同时删除你的 OpenSense 设置和日志？"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "一些 OpenSense 文件正在使用中，将在 Windows 重新启动时删除。"
