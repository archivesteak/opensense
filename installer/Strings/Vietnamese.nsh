; OpenSense's own installer text in Vietnamese, next to the app's Strings\vi-VN.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Vietnamese"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "Khởi động OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "Bạn không cần chấp nhận giấy phép này để sử dụng OpenSense. Giấy phép cho bạn quyền chia sẻ và thay đổi chương trình, đồng thời đặt ra điều kiện để làm việc đó."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (bắt buộc)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "Trình điều khiển PawnIO (bắt buộc)"
; Component name.
${LangFileString} Inst_SecStartMenu "Lối tắt trong menu Bắt đầu"
; Component name.
${LangFileString} Inst_SecDesktop "Lối tắt trên màn hình nền"
; Component name.
${LangFileString} Inst_SecAutostart "Khởi động cùng Windows"
; Component description.
${LangFileString} Inst_DescCore "Ứng dụng OpenSense và dịch vụ chạy nền của nó, cùng mọi thứ cần để chạy."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "Trình điều khiển đã ký cho phép OpenSense đọc cảm biến nhiệt độ của chính CPU, giống như ThrottleStop và HWiNFO. Bỏ qua nếu PawnIO đã được cài đặt."
; Component description.
${LangFileString} Inst_DescStartMenu "Thêm OpenSense vào menu Bắt đầu."
; Component description.
${LangFileString} Inst_DescDesktop "Thêm lối tắt OpenSense vào màn hình nền."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "Mở OpenSense trong vùng thông báo khi bạn đăng nhập. Dù thế nào, điều khiển quạt vẫn chạy từ khi khởi động."
; Progress log line.
${LangFileString} Inst_Closing "Đang đóng OpenSense đang chạy..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "Đang gỡ bỏ phiên bản đã cài đặt khỏi $0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "Không thể gỡ bỏ phiên bản OpenSense đã cài đặt (lỗi $1). Vẫn tiếp tục?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 đã được cài đặt."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "Đang cài đặt PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "Không thể cài đặt trình điều khiển PawnIO (lỗi $1). Cho đến khi cài được, OpenSense sẽ hiển thị nhiệt độ CPU kém chính xác hơn từ chương trình cơ sở."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "Đang đăng ký dịch vụ OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "Không thể đăng ký dịch vụ OpenSense (lỗi $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "Đang khởi động dịch vụ OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "Dịch vụ OpenSense không khởi động được (lỗi $0). Nhật ký của dịch vụ nằm trong %ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Điều khiển quạt, hiệu năng và đèn cho máy tính xách tay Acer Nitro và Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "Điều khiển quạt, hiệu năng và đèn"
${LangFileString} Inst_Needs64Bit "OpenSense yêu cầu Windows 64 bit."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense yêu cầu Windows 10 phiên bản 2004 (bản dựng ${MIN_WINDOWS_BUILD}) trở lên."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "Đang dừng dịch vụ OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "Gỡ bỏ cả cài đặt và nhật ký OpenSense của bạn?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "Một số tệp OpenSense đang được sử dụng và sẽ được gỡ bỏ khi Windows khởi động lại."
