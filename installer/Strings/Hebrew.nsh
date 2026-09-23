; OpenSense's own installer text in Hebrew, next to the app's Strings\he-IL.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Hebrew"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "הפעל את OpenSense"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "אין צורך לקבל את הרישיון הזה כדי להשתמש ב-OpenSense. הוא מעניק לך את הזכות לשתף ולשנות אותו, וקובע את התנאים לכך."
; Component name.
${LangFileString} Inst_SecCore "OpenSense (חובה)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "מנהל ההתקן PawnIO (חובה)"
; Component name.
${LangFileString} Inst_SecStartMenu "קיצור דרך בתפריט 'התחל'"
; Component name.
${LangFileString} Inst_SecDesktop "קיצור דרך בשולחן העבודה"
; Component name.
${LangFileString} Inst_SecAutostart "הפעל עם Windows"
; Component description.
${LangFileString} Inst_DescCore "אפליקציית OpenSense ושירות הרקע שלה, עם כל מה שהם צריכים כדי לפעול."
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "מנהל התקן חתום שמאפשר ל-OpenSense לקרוא את חיישן הטמפרטורה של ה-CPU עצמו, כמו ThrottleStop ו-HWiNFO. המערכת מדלגת עליו אם PawnIO כבר מותקן."
; Component description.
${LangFileString} Inst_DescStartMenu "מוסיף את OpenSense לתפריט 'התחל'."
; Component description.
${LangFileString} Inst_DescDesktop "מוסיף קיצור דרך ל-OpenSense בשולחן העבודה."
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "פותח את OpenSense באזור ההודעות בעת הכניסה. בקרת המאווררים פועלת מרגע ההפעלה בכל מקרה."
; Progress log line.
${LangFileString} Inst_Closing "סוגר את OpenSense הפועל..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "מסיר את הגרסה המותקנת מ-$0..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "לא ניתן היה להסיר את הגרסה המותקנת של OpenSense (שגיאה $1). להמשיך בכל זאת?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 כבר מותקן."
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "מתקין את PawnIO..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "לא ניתן היה להתקין את מנהל ההתקן PawnIO (שגיאה $1). עד שהוא יותקן, OpenSense יציג את טמפרטורת ה-CPU הפחות מדויקת של הקושחה."
; Progress log line.
${LangFileString} Inst_ServiceRegistering "רושם את השירות של OpenSense..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "לא ניתן היה לרשום את השירות של OpenSense (שגיאה $0)."
; Progress log line.
${LangFileString} Inst_ServiceStarting "מפעיל את השירות של OpenSense..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "השירות של OpenSense לא הופעל (שגיאה $0). היומן שלו נמצא ב-%ProgramData%\OpenSense\logs."
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "בקרת מאווררים, ביצועים ותאורה למחשבים הניידים Acer Nitro ו-Predator."
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "בקרת מאווררים, ביצועים ותאורה"
${LangFileString} Inst_Needs64Bit "OpenSense דורש גרסת 64 סיביות של Windows."
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense דורש Windows 10 גרסה 2004 (Build ${MIN_WINDOWS_BUILD}) ואילך."
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "עוצר את השירות של OpenSense..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "להסיר גם את ההגדרות והיומנים של OpenSense?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "חלק מהקבצים של OpenSense נמצאים בשימוש ויוסרו כש-Windows יופעל מחדש."
