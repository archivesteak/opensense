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
!define MUI_FINISHPAGE_RUN_TEXT "Start ${APP_NAME}"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp

!insertmacro MUI_PAGE_WELCOME
; The GPL is not an agreement to accept: it grants the rights to share and change OpenSense.
!define MUI_LICENSEPAGE_TEXT_BOTTOM "You don't need to accept this license to use ${APP_NAME}. It gives you the right to share and change it, and sets the conditions for doing so."
!define MUI_LICENSEPAGE_BUTTON "$(^NextBtn)"
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; Asks the running window to quit before its files change.
!macro StopRunningCopy
  ${If} ${FileExists} "$INSTDIR\${APP_EXE}"
    DetailPrint "Closing the running copy of ${APP_NAME}..."
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

  DetailPrint "Removing the installed version from $0..."
  ClearErrors
  ; _?= runs the uninstaller in place instead of from a temporary copy, so ExecWait really waits.
  ExecWait '"$0\Uninstall.exe" /S _?=$0' $1
  ${If} ${Errors}
  ${OrIf} $1 != 0
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "The installed version of ${APP_NAME} could not be removed (error $1). Continue anyway?" /SD IDYES IDYES +2
      Abort
  ${EndIf}
  ; A running uninstaller cannot delete itself.
  Delete "$0\Uninstall.exe"
  RMDir "$0"
SectionEnd

Section "${APP_NAME} (required)" SecCore
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

Section "PawnIO driver (required)" SecPawnIO
  SectionIn RO
  ; Shared with other tools (LibreHardwareMonitor, FanControl), so an existing install is used as it is.
  ReadRegStr $0 HKLM "${PAWNIO_KEY}" "DisplayVersion"
  ${If} $0 != ""
    DetailPrint "PawnIO $0 is already installed."
    Return
  ${EndIf}

  DetailPrint "Installing PawnIO..."
  InitPluginsDir
  File "/oname=$PLUGINSDIR\PawnIO_setup.exe" "${PAWNIO_SETUP}"
  ExecWait '"$PLUGINSDIR\PawnIO_setup.exe" -install -silent' $1
  ${If} $1 != 0
    MessageBox MB_OK|MB_ICONEXCLAMATION "The PawnIO driver could not be installed (error $1). ${APP_NAME} will show the firmware's less accurate CPU temperature until it is." /SD IDOK
  ${EndIf}
SectionEnd

; The service owns the laptop's firmware as LocalSystem from boot, so the app needs no administrator
; rights. After PawnIO, so its first start finds the driver.
Section "-Service"
  DetailPrint "Registering the ${APP_NAME} service..."
  nsExec::ExecToLog '"$SYSDIR\sc.exe" create ${SERVICE_NAME} binPath= "\"$INSTDIR\${SERVICE_EXE}\"" start= auto depend= Winmgmt DisplayName= "${APP_NAME}"'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_OK|MB_ICONSTOP "The ${APP_NAME} service could not be registered (error $0)." /SD IDOK
    Abort
  ${EndIf}
  nsExec::ExecToLog '"$SYSDIR\sc.exe" description ${SERVICE_NAME} "Fan, performance and lighting control for Acer Nitro and Predator laptops."'
  Pop $0
  ; Restart after a crash: 5 s, 10 s, then every minute; the count resets after a day.
  nsExec::ExecToLog '"$SYSDIR\sc.exe" failure ${SERVICE_NAME} reset= 86400 actions= restart/5000/restart/10000/restart/60000'
  Pop $0

  DetailPrint "Starting the ${APP_NAME} service..."
  nsExec::ExecToLog '"$SYSDIR\net.exe" start ${SERVICE_NAME}'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_OK|MB_ICONEXCLAMATION "The ${APP_NAME} service did not start (error $0). Its log is in %ProgramData%\${APP_NAME}\logs." /SD IDOK
  ${EndIf}
SectionEnd

Section "Start menu shortcut" SecStartMenu
  SetShellVarContext all
  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "Fan, performance and lighting control"
SectionEnd

Section "Desktop shortcut" SecDesktop
  SetShellVarContext all
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "Start with Windows" SecAutostart
  ; A per-user Run entry for the notification area icon; fan control runs from boot regardless.
  ExecWait '"$INSTDIR\${APP_EXE}" --enable-autostart'
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "The OpenSense app and its background service, with everything they need to run."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecPawnIO} "Signed driver that lets OpenSense read the CPU's own temperature sensor, like ThrottleStop and HWiNFO. Skipped if PawnIO is already installed."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} "Adds OpenSense to the Start menu."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Adds an OpenSense shortcut to the desktop."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAutostart} "Opens OpenSense in the notification area when you sign in. Fan control runs from startup either way."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; --- checks and defaults (after the sections, so their ids are defined) ---------------------

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requires 64-bit Windows." /SD IDOK
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
  ${If} $0 < ${MIN_WINDOWS_BUILD}
    MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requires Windows 10 version 2004 (build ${MIN_WINDOWS_BUILD}) or later." /SD IDOK
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
  SetRegView 64
FunctionEnd

; --- uninstaller ----------------------------------------------------------------------------

Section "Uninstall"
  !insertmacro StopRunningCopy
  ${If} ${FileExists} "$INSTDIR\${APP_EXE}"
    ExecWait '"$INSTDIR\${APP_EXE}" --disable-autostart'
  ${EndIf}
  DetailPrint "Stopping the ${APP_NAME} service..."
  !insertmacro RemoveService

  SetShellVarContext all
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  ; Exactly the files setup installed (listed at build time), never the whole folder: it may hold
  ; other things if the user picked an existing folder. Files another user's open window still
  ; holds go at the next restart.
  !include "${UNINSTALL_LIST}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /REBOOTOK "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"

  MessageBox MB_YESNO|MB_ICONQUESTION "Also remove your ${APP_NAME} settings and logs?" /SD IDNO IDNO keep_user_data
    SetShellVarContext all
    RMDir /r "$APPDATA\${APP_NAME}" ; %ProgramData%: the service's settings and logs
    SetShellVarContext current
    RMDir /r "$APPDATA\${APP_NAME}"
    RMDir /r "$LOCALAPPDATA\${APP_NAME}"
  keep_user_data:

  ${If} ${RebootFlag}
    MessageBox MB_OK|MB_ICONINFORMATION "Some ${APP_NAME} files are in use and will be removed when Windows restarts." /SD IDOK
  ${EndIf}
SectionEnd
