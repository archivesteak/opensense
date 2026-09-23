; OpenSense's own installer text in Korean, next to the app's Strings\ko-KR.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Korean"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "OpenSense 시작"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "OpenSense를 사용하기 위해 이 라이선스에 동의할 필요는 없습니다. 이 라이선스는 프로그램을 공유하고 수정할 권리를 부여하며 그 조건을 정합니다."
; Component name.
${LangFileString} Inst_SecCore "OpenSense(필수)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO 드라이버(필수)"
; Component name.
${LangFileString} Inst_SecStartMenu "시작 메뉴 바로 가기"
; Component name.
${LangFileString} Inst_SecDesktop "바탕 화면 바로 가기"
; Component name.
${LangFileString} Inst_SecAutostart "Windows와 함께 시작"
; Component description.
${LangFileString} Inst_DescCore "OpenSense 앱과 백그라운드 서비스, 그리고 실행에 필요한 모든 구성 요소입니다."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "ThrottleStop 및 HWiNFO처럼 OpenSense가 CPU 자체의 온도 센서를 읽을 수 있게 해 주는 서명된 드라이버입니다. PawnIO가 이미 설치되어 있으면 건너뜁니다."
; Component description.
${LangFileString} Inst_DescStartMenu "OpenSense를 시작 메뉴에 추가합니다."
; Component description.
${LangFileString} Inst_DescDesktop "OpenSense 바로 가기를 바탕 화면에 추가합니다."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "로그인할 때 알림 영역에서 OpenSense를 엽니다. 팬 제어는 어떤 경우든 시작 시부터 작동합니다."
; Progress log line.
${LangFileString} Inst_Closing "실행 중인 OpenSense를 닫는 중..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "$0에서 설치된 버전을 제거하는 중..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "설치된 OpenSense 버전을 제거할 수 없습니다(오류 $1). 그래도 계속하시겠습니까?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0이(가) 이미 설치되어 있습니다."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "PawnIO를 설치하는 중..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "PawnIO 드라이버를 설치할 수 없습니다(오류 $1). 설치될 때까지 OpenSense는 펌웨어가 제공하는 정확도가 낮은 CPU 온도를 표시합니다."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "OpenSense 서비스를 등록하는 중..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "OpenSense 서비스를 등록할 수 없습니다(오류 $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "OpenSense 서비스를 시작하는 중..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense 서비스가 시작되지 않았습니다(오류 $0). 로그는 %ProgramData%\OpenSense\logs에 있습니다."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Acer Nitro 및 Predator 노트북을 위한 팬, 성능 및 조명 제어입니다."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "팬, 성능 및 조명 제어"
${LangFileString} Inst_Needs64Bit "OpenSense를 사용하려면 64비트 Windows가 필요합니다."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense를 사용하려면 Windows 10 버전 2004(빌드 ${MIN_WINDOWS_BUILD}) 이상이 필요합니다."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "OpenSense 서비스를 중지하는 중..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "OpenSense 설정과 로그도 제거하시겠습니까?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "일부 OpenSense 파일이 사용 중이므로 Windows를 다시 시작할 때 제거됩니다."
