; OpenSense's own installer text in Japanese, next to the app's Strings\ja-JP.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Japanese"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "OpenSense を起動"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "OpenSense を使用するためにこのライセンスに同意する必要はありません。このライセンスは、ソフトウェアを共有および変更する権利と、その条件を定めています。"
; Component name.
${LangFileString} Inst_SecCore "OpenSense（必須）"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO ドライバー（必須）"
; Component name.
${LangFileString} Inst_SecStartMenu "スタート メニューのショートカット"
; Component name.
${LangFileString} Inst_SecDesktop "デスクトップのショートカット"
; Component name.
${LangFileString} Inst_SecAutostart "Windows と同時に起動"
; Component description.
${LangFileString} Inst_DescCore "OpenSense アプリとそのバックグラウンド サービス、および実行に必要なすべてのものです。"
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "ThrottleStop や HWiNFO と同じように、OpenSense が CPU 自体の温度センサーを読み取れるようにする署名済みドライバーです。PawnIO がすでにインストールされている場合はスキップされます。"
; Component description.
${LangFileString} Inst_DescStartMenu "OpenSense をスタート メニューに追加します。"
; Component description.
${LangFileString} Inst_DescDesktop "OpenSense のショートカットをデスクトップに追加します。"
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "サインイン時に OpenSense を通知領域で開きます。いずれにしてもファン制御は起動時から動作します。"
; Progress log line.
${LangFileString} Inst_Closing "実行中の OpenSense を終了しています..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "インストール済みのバージョンを $0 から削除しています..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "インストール済みの OpenSense を削除できませんでした（エラー $1）。続行しますか？"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 はすでにインストールされています。"
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "PawnIO をインストールしています..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "PawnIO ドライバーをインストールできませんでした（エラー $1）。インストールされるまで、OpenSense はファームウェアによる精度の低い CPU 温度を表示します。"
; Progress log line.
${LangFileString} Inst_ServiceRegistering "OpenSense サービスを登録しています..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "OpenSense サービスを登録できませんでした（エラー $0）。"
; Progress log line.
${LangFileString} Inst_ServiceStarting "OpenSense サービスを開始しています..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense サービスが開始されませんでした（エラー $0）。ログは %ProgramData%\OpenSense\logs にあります。"
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Acer Nitro と Predator のノート PC 向けのファン、パフォーマンス、ライティング制御です。"
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "ファン、パフォーマンス、ライティングの制御"
${LangFileString} Inst_Needs64Bit "OpenSense には 64 ビット版の Windows が必要です。"
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense には Windows 10 バージョン 2004（ビルド ${MIN_WINDOWS_BUILD}）以降が必要です。"
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "OpenSense サービスを停止しています..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "OpenSense の設定とログも削除しますか？"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "一部の OpenSense ファイルは使用中のため、Windows の再起動時に削除されます。"
