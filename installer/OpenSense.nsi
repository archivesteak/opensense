; OpenSense installer (NSIS 3.x, Modern UI 2).
; Built by CI (.github/workflows/build.yml), which prepares the inputs and runs:
;   makensis /DVERSION=x.y.z /DPUBLISH_DIR=<folder> /DUNINSTALL_LIST=<file.nsh> /DPAWNIO_SETUP=<PawnIO_setup.exe>
;            /DOUTPUT=<setup.exe> OpenSense.nsi
;
; Command line: /S (silent), /D=<folder>, /NoShortcuts (no Start menu or desktop shortcut),
;   /Relaunch (open OpenSense when done; used by the in-app updater).

Unicode true
ManifestDPIAware true
ManifestSupportedOS all
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64

!ifndef VERSION
  !error "Pass /DVERSION=<major.minor.patch>"
!endif
!ifndef PUBLISH_DIR
  !error "Pass /DPUBLISH_DIR=<folder containing the published app>"
!endif
!ifndef UNINSTALL_LIST
  !error "Pass /DUNINSTALL_LIST=<.nsh listing the published files>"
!endif
!ifndef PAWNIO_SETUP
  !error "Pass /DPAWNIO_SETUP=<path to PawnIO_setup.exe>"
!endif
!ifndef OUTPUT
  !define OUTPUT "OpenSense-${VERSION}-Setup-x64.exe"
!endif

!define APP_NAME "OpenSense"
!define APP_EXE "OpenSense.exe"
!define SERVICE_NAME "OpenSense"
!define SERVICE_EXE "OpenSense.Service.exe"
!define PUBLISHER "OpenSense contributors"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenSense"
!define PAWNIO_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"
!define MIN_WINDOWS_BUILD 19041

!include MUI2.nsh
!include x64.nsh
!include LogicLib.nsh
!include Sections.nsh
!include FileFunc.nsh
!include WordFunc.nsh

Name "${APP_NAME}"
OutFile "${OUTPUT}"
; An existing install's folder is picked up in .onInit (InstallDirRegKey would read the 32-bit registry view).
InstallDir "$PROGRAMFILES64\${APP_NAME}"
BrandingText "${APP_NAME} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} setup"
VIAddVersionKey "LegalCopyright" "OpenSense contributors. GNU GPL v3 or later."

; --- pages ----------------------------------------------------------------------------------

!define MUI_ICON "..\src\OpenSense.App\Assets\OpenSense.ico"
!define MUI_UNICON "..\src\OpenSense.App\Assets\OpenSense.ico"
!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_SMALLDESC
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "$(Inst_Run)"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp

!insertmacro MUI_PAGE_WELCOME
; The GPL is not an agreement to accept: it grants the rights to share and change OpenSense.
!define MUI_LICENSEPAGE_TEXT_BOTTOM "$(Inst_LicenseNote)"
!define MUI_LICENSEPAGE_BUTTON "$(^NextBtn)"
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; --- languages ------------------------------------------------------------------------------

; The app's languages (src\OpenSense.App\Strings), each with its own text in Strings\<language>.nsh. A string
; missing there falls back to English, with a warning at build time.
!macro OpenSenseLanguage NLFID
  !insertmacro MUI_LANGUAGE "${NLFID}"
  !insertmacro LANGFILE_INCLUDE_WITHDEFAULT "Strings\${NLFID}.nsh" "Strings\English.nsh"
!macroend

; English first: setup runs in it when no other language matches Windows' display language.
!insertmacro OpenSenseLanguage "English"
!insertmacro OpenSenseLanguage "Arabic"
!insertmacro OpenSenseLanguage "Bulgarian"
!insertmacro OpenSenseLanguage "Catalan"
!insertmacro OpenSenseLanguage "Czech"
!insertmacro OpenSenseLanguage "Dutch"
!insertmacro OpenSenseLanguage "Estonian"
!insertmacro OpenSenseLanguage "Farsi"
!insertmacro OpenSenseLanguage "Finnish"
!insertmacro OpenSenseLanguage "French"
!insertmacro OpenSenseLanguage "German"
!insertmacro OpenSenseLanguage "Greek"
!insertmacro OpenSenseLanguage "Hebrew"
!insertmacro OpenSenseLanguage "Hindi"
!insertmacro OpenSenseLanguage "Hungarian"
!insertmacro OpenSenseLanguage "Indonesian"
!insertmacro OpenSenseLanguage "Italian"
!insertmacro OpenSenseLanguage "Japanese"
!insertmacro OpenSenseLanguage "Korean"
!insertmacro OpenSenseLanguage "Latvian"
!insertmacro OpenSenseLanguage "Norwegian"
!insertmacro OpenSenseLanguage "Polish"
!insertmacro OpenSenseLanguage "Portuguese"
!insertmacro OpenSenseLanguage "PortugueseBR"
!insertmacro OpenSenseLanguage "Romanian"
!insertmacro OpenSenseLanguage "Russian"
!insertmacro OpenSenseLanguage "Serbian"
!insertmacro OpenSenseLanguage "SimpChinese"
!insertmacro OpenSenseLanguage "Slovak"
!insertmacro OpenSenseLanguage "Spanish"
!insertmacro OpenSenseLanguage "Swedish"
!insertmacro OpenSenseLanguage "Thai"
!insertmacro OpenSenseLanguage "TradChinese"
!insertmacro OpenSenseLanguage "Turkish"
!insertmacro OpenSenseLanguage "Ukrainian"
!insertmacro OpenSenseLanguage "Vietnamese"

; NSIS runs in Windows' display language or, failing that, in any language sharing its primary language,
; whatever the script. Chinese needs the script its region uses, and Croatian, Bosnian and Latin-script
; Serbian must not get Cyrillic Serbian; this settles them as the app does.
!macro SettleLanguage
  System::Call 'kernel32::GetUserDefaultUILanguage() i .r0'
  IntOp $1 $0 & 0x3FF
  ${If} $1 = 0x04 ; Chinese: Simplified in mainland China and Singapore, Traditional elsewhere
    ${If} $0 = 0x0804
    ${OrIf} $0 = 0x1004
      StrCpy $LANGUAGE ${LANG_SIMPCHINESE}
    ${Else}
      StrCpy $LANGUAGE ${LANG_TRADCHINESE}
    ${EndIf}
  ${ElseIf} $1 = 0x1A ; Serbian, Croatian and Bosnian: only Serbian in Cyrillic is translated
    ${If} $0 = 0x0C1A
    ${OrIf} $0 = 0x1C1A
    ${OrIf} $0 = 0x281A
    ${OrIf} $0 = 0x301A
      StrCpy $LANGUAGE ${LANG_SERBIAN}
    ${Else}
      StrCpy $LANGUAGE ${LANG_ENGLISH}
    ${EndIf}
  ${EndIf}
!macroend

; Asks the running window to quit before its files change.
!macro StopRunningCopy
  ${If} ${FileExists} "$INSTDIR\${APP_EXE}"
    DetailPrint "$(Inst_Closing)"
    ExecWait '"$INSTDIR\${APP_EXE}" --exit'
  ${EndIf}
!macroend

; Stops the service (it hands the fans back to the firmware) and unregisters it. Quiet when it is not there.
; net stop waits for the service to stop, unlike sc stop.
!macro RemoveService
  nsExec::Exec '"$SYSDIR\net.exe" stop ${SERVICE_NAME}'
  Pop $0
  nsExec::Exec '"$SYSDIR\sc.exe" delete ${SERVICE_NAME}'
  Pop $0
!macroend

; Sets _RESULT to 1 when _VALUE has the form {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}, else 0.
!macro IsGuid _VALUE _RESULT
  StrCpy ${_RESULT} 0
  StrLen $R9 "${_VALUE}"
  ${If} $R9 = 38
    StrCpy $R8 "${_VALUE}" 1
    StrCpy $R7 "${_VALUE}" 1 -1
    ${If} $R8 == "{"
    ${AndIf} $R7 == "}"
      StrCpy ${_RESULT} 1
    ${EndIf}
  ${EndIf}
!macroend

; The installed app registers for app notifications (Windows App SDK) for each user who runs it: a key named after
; its path ("\" written as ".") holds a random app id, which names the app's key (with a random COM class for clicks)
; and the keys Windows keeps for it. Removed for the user running the uninstaller; only keys named by the ids read
; from that registration are touched.
!macro RemoveNotificationRegistration
  ${WordReplace} "$INSTDIR\${APP_EXE}" "\" "." "+" $R0
  StrCpy $R0 "Software\Classes\AppUserModelId\$R0"
  ReadRegStr $R1 HKCU "$R0" "NotificationGUID"
  !insertmacro IsGuid $R1 $R3
  ${If} $R3 = 1
    ReadRegStr $R2 HKCU "Software\Classes\AppUserModelId\$R1" "CustomActivator"
    !insertmacro IsGuid $R2 $R3
    ${If} $R3 = 1
      DeleteRegKey HKCU "Software\Classes\CLSID\$R2"
    ${EndIf}
    DeleteRegKey HKCU "Software\Classes\AppUserModelId\$R1"
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\PushNotifications\Backup\$R1"
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\$R1"
  ${EndIf}
  DeleteRegKey HKCU "$R0"
!macroend

Function LaunchApp
  ; Setup runs as administrator and the app does not need to: Explorer starts it as the signed-in user.
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP_EXE}"'
FunctionEnd

; --- sections -------------------------------------------------------------------------------

; An installed version is removed first by its own uninstaller; settings and logs are kept.
Section "-Remove installed version"
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 == ""
  ${OrIfNot} ${FileExists} "$0\Uninstall.exe"
    Return
  ${EndIf}

  DetailPrint "$(Inst_RemovingOld)"
  ; /Update (same folder only) keeps what the new version takes over as it is: the users' app-notification
  ; registration and their Windows notification settings for OpenSense.
  StrCpy $2 ""
  ${If} $0 == $INSTDIR
    StrCpy $2 "/Update "
  ${EndIf}
  ClearErrors
  ; _?= runs the uninstaller in place instead of from a temporary copy, so ExecWait really waits.
  ExecWait '"$0\Uninstall.exe" /S $2_?=$0' $1
  ${If} ${Errors}
  ${OrIf} $1 != 0
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "$(Inst_RemoveFailed)" /SD IDYES IDYES +2
      Abort
  ${EndIf}
  ; A running uninstaller cannot delete itself.
  Delete "$0\Uninstall.exe"
  RMDir "$0"
SectionEnd

Section "$(Inst_SecCore)" SecCore
  SectionIn RO
  SetOutPath "$INSTDIR"
  !insertmacro StopRunningCopy
  !insertmacro RemoveService ; normally gone with the previous version; this covers a failed uninstall

  File /r "${PUBLISH_DIR}\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE},0"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "$(Inst_SecPawnIO)" SecPawnIO
  SectionIn RO
  ; Shared with other tools (LibreHardwareMonitor, FanControl), so an existing install is used as it is.
  ReadRegStr $0 HKLM "${PAWNIO_KEY}" "DisplayVersion"
  ${If} $0 != ""
    DetailPrint "$(Inst_PawnIOPresent)"
    Return
  ${EndIf}

  DetailPrint "$(Inst_PawnIOInstalling)"
  InitPluginsDir
  File "/oname=$PLUGINSDIR\PawnIO_setup.exe" "${PAWNIO_SETUP}"
  ExecWait '"$PLUGINSDIR\PawnIO_setup.exe" -install -silent' $1
  ${If} $1 != 0
    MessageBox MB_OK|MB_ICONEXCLAMATION "$(Inst_PawnIOFailed)" /SD IDOK
  ${EndIf}
SectionEnd

; The service owns the laptop's firmware as LocalSystem from boot, so the app needs no administrator
; rights. After PawnIO, so its first start finds the driver.
Section "-Service"
  DetailPrint "$(Inst_ServiceRegistering)"
  nsExec::ExecToLog '"$SYSDIR\sc.exe" create ${SERVICE_NAME} binPath= "\"$INSTDIR\${SERVICE_EXE}\"" start= auto depend= Winmgmt DisplayName= "${APP_NAME}"'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_OK|MB_ICONSTOP "$(Inst_ServiceRegisterFailed)" /SD IDOK
    Abort
  ${EndIf}
  nsExec::ExecToLog '"$SYSDIR\sc.exe" description ${SERVICE_NAME} "$(Inst_ServiceDescription)"'
  Pop $0
  ; Restart after a crash: 5 s, 10 s, then every minute; the count resets after a day.
  nsExec::ExecToLog '"$SYSDIR\sc.exe" failure ${SERVICE_NAME} reset= 86400 actions= restart/5000/restart/10000/restart/60000'
  Pop $0

  DetailPrint "$(Inst_ServiceStarting)"
  nsExec::ExecToLog '"$SYSDIR\net.exe" start ${SERVICE_NAME}'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_OK|MB_ICONEXCLAMATION "$(Inst_ServiceStartFailed)" /SD IDOK
  ${EndIf}
SectionEnd

Section "$(Inst_SecStartMenu)" SecStartMenu
  SetShellVarContext all
  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "$(Inst_ShortcutDescription)"
SectionEnd

Section "$(Inst_SecDesktop)" SecDesktop
  SetShellVarContext all
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "$(Inst_SecAutostart)" SecAutostart
  ; A per-user Run entry for the notification area icon; fan control runs from boot regardless.
  ExecWait '"$INSTDIR\${APP_EXE}" --enable-autostart'
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "$(Inst_DescCore)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecPawnIO} "$(Inst_DescPawnIO)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} "$(Inst_DescStartMenu)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "$(Inst_DescDesktop)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAutostart} "$(Inst_DescAutostart)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; --- checks and defaults (after the sections, so their ids are defined) ---------------------

Function .onInit
  !insertmacro SettleLanguage
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "$(Inst_Needs64Bit)" /SD IDOK
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
  ${If} $0 < ${MIN_WINDOWS_BUILD}
    MessageBox MB_OK|MB_ICONSTOP "$(Inst_NeedsWindows)" /SD IDOK
    Abort
  ${EndIf}

  ; Updating: install into the same folder (unless /D= chose another) and keep the choices made last
  ; time for the optional parts.
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 != ""
    ${If} $INSTDIR == "$PROGRAMFILES64\${APP_NAME}"
      StrCpy $INSTDIR $0
    ${EndIf}
    SetShellVarContext all
    ${IfNot} ${FileExists} "$SMPROGRAMS\${APP_NAME}.lnk"
      !insertmacro UnselectSection ${SecStartMenu}
    ${EndIf}
    ${IfNot} ${FileExists} "$DESKTOP\${APP_NAME}.lnk"
      !insertmacro UnselectSection ${SecDesktop}
    ${EndIf}
    ReadRegStr $1 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"
    ${If} $1 == ""
      !insertmacro UnselectSection ${SecAutostart}
    ${EndIf}
  ${EndIf}

  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/NoShortcuts" $R1
  ${IfNot} ${Errors}
    !insertmacro UnselectSection ${SecStartMenu}
    !insertmacro UnselectSection ${SecDesktop}
  ${EndIf}
FunctionEnd

; An update started from the app (setup /S /Relaunch): open OpenSense again once installed.
Function .onInstSuccess
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/Relaunch" $R1
  ${IfNot} ${Errors}
    Call LaunchApp
  ${EndIf}
FunctionEnd

Function un.onInit
  !insertmacro SettleLanguage
  SetRegView 64
FunctionEnd

; --- uninstaller ----------------------------------------------------------------------------

Section "Uninstall"
  !insertmacro StopRunningCopy
  ${If} ${FileExists} "$INSTDIR\${APP_EXE}"
    ExecWait '"$INSTDIR\${APP_EXE}" --disable-autostart'
  ${EndIf}
  DetailPrint "$(Inst_ServiceStopping)"
  !insertmacro RemoveService

  SetShellVarContext all
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/Update" $R1
  ${If} ${Errors}
    !insertmacro RemoveNotificationRegistration
  ${EndIf}

  ; Exactly the files setup installed (listed at build time), never the whole folder: it may hold
  ; other things if the user picked an existing folder. Files another user's open window still
  ; holds go at the next restart.
  !include "${UNINSTALL_LIST}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /REBOOTOK "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"

  MessageBox MB_YESNO|MB_ICONQUESTION "$(Inst_RemoveUserData)" /SD IDNO IDNO keep_user_data
    SetShellVarContext all
    RMDir /r "$APPDATA\${APP_NAME}" ; %ProgramData%: the service's settings and logs
    SetShellVarContext current
    RMDir /r "$APPDATA\${APP_NAME}"
    RMDir /r "$LOCALAPPDATA\${APP_NAME}"
  keep_user_data:

  ${If} ${RebootFlag}
    MessageBox MB_OK|MB_ICONINFORMATION "$(Inst_RebootToFinish)" /SD IDOK
  ${EndIf}
SectionEnd
