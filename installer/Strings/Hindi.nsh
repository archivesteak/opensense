; OpenSense's own installer text in Hindi, next to the app's Strings\hi-IN.
; $0, $1 and ${MIN_WINDOWS_BUILD} are filled in by the installer: keep them as they are.

!insertmacro LANGFILE_EXT "Hindi"

; Check box on the last page: open the app when setup closes.
${LangFileString} Inst_Run "OpenSense प्रारंभ करें"
; Under the GPL text on the license page.
${LangFileString} Inst_LicenseNote "OpenSense का उपयोग करने के लिए आपको यह लाइसेंस स्वीकार करने की आवश्यकता नहीं है। यह आपको इसे साझा करने और बदलने का अधिकार देता है, और ऐसा करने की शर्तें तय करता है।"
; Component name.
${LangFileString} Inst_SecCore "OpenSense (आवश्यक)"
; Component name. PawnIO is a driver's name: keep it.
${LangFileString} Inst_SecPawnIO "PawnIO ड्राइवर (आवश्यक)"
; Component name.
${LangFileString} Inst_SecStartMenu "प्रारंभ मेनू शॉर्टकट"
; Component name.
${LangFileString} Inst_SecDesktop "डेस्कटॉप शॉर्टकट"
; Component name.
${LangFileString} Inst_SecAutostart "Windows के साथ प्रारंभ करें"
; Component description.
${LangFileString} Inst_DescCore "OpenSense ऐप और उसकी पृष्ठभूमि सेवा, चलने के लिए ज़रूरी हर चीज़ के साथ।"
; Component description. ThrottleStop and HWiNFO are app names.
${LangFileString} Inst_DescPawnIO "हस्ताक्षरित ड्राइवर जो OpenSense को CPU का अपना तापमान सेंसर पढ़ने देता है, ThrottleStop और HWiNFO की तरह। यदि PawnIO पहले से इंस्टॉल है तो छोड़ दिया जाता है।"
; Component description.
${LangFileString} Inst_DescStartMenu "OpenSense को प्रारंभ मेनू में जोड़ता है।"
; Component description.
${LangFileString} Inst_DescDesktop "डेस्कटॉप पर OpenSense शॉर्टकट जोड़ता है।"
; Component description. Notification area = the Windows system tray.
${LangFileString} Inst_DescAutostart "साइन इन करने पर सूचना क्षेत्र में OpenSense खोलता है। पंखा नियंत्रण हर हाल में स्टार्टअप से चलता है।"
; Progress log line.
${LangFileString} Inst_Closing "चल रहे OpenSense को बंद किया जा रहा है..."
; Progress log line. $0 = a folder.
${LangFileString} Inst_RemovingOld "इंस्टॉल किया गया संस्करण $0 से हटाया जा रहा है..."
; Yes/No question. $1 = an error code.
${LangFileString} Inst_RemoveFailed "OpenSense का इंस्टॉल किया गया संस्करण हटाया नहीं जा सका (त्रुटि $1)। फिर भी जारी रखें?"
; Progress log line. $0 = a version number.
${LangFileString} Inst_PawnIOPresent "PawnIO $0 पहले से इंस्टॉल है।"
; Progress log line.
${LangFileString} Inst_PawnIOInstalling "PawnIO इंस्टॉल हो रहा है..."
; $1 = an error code.
${LangFileString} Inst_PawnIOFailed "PawnIO ड्राइवर इंस्टॉल नहीं हो सका (त्रुटि $1)। इसके इंस्टॉल होने तक OpenSense फ़र्मवेयर का कम सटीक CPU तापमान दिखाएगा।"
; Progress log line.
${LangFileString} Inst_ServiceRegistering "OpenSense सेवा पंजीकृत की जा रही है..."
; $0 = an error code.
${LangFileString} Inst_ServiceRegisterFailed "OpenSense सेवा पंजीकृत नहीं की जा सकी (त्रुटि $0)।"
; Progress log line.
${LangFileString} Inst_ServiceStarting "OpenSense सेवा प्रारंभ हो रही है..."
; $0 = an error code. Keep the folder path.
${LangFileString} Inst_ServiceStartFailed "OpenSense सेवा प्रारंभ नहीं हुई (त्रुटि $0)। इसका लॉग %ProgramData%\OpenSense\logs में है।"
; The service's description in the Windows Services list.
${LangFileString} Inst_ServiceDescription "Acer Nitro और Predator लैपटॉप के लिए पंखा, प्रदर्शन और लाइटिंग नियंत्रण।"
; The Start menu shortcut's tooltip.
${LangFileString} Inst_ShortcutDescription "पंखा, प्रदर्शन और लाइटिंग नियंत्रण"
${LangFileString} Inst_Needs64Bit "OpenSense के लिए 64-बिट Windows आवश्यक है।"
; ${MIN_WINDOWS_BUILD} = a Windows build number.
${LangFileString} Inst_NeedsWindows "OpenSense के लिए Windows 10 संस्करण 2004 (बिल्ड ${MIN_WINDOWS_BUILD}) या बाद का संस्करण आवश्यक है।"
; Progress log line (uninstall).
${LangFileString} Inst_ServiceStopping "OpenSense सेवा रोकी जा रही है..."
; Yes/No question when uninstalling.
${LangFileString} Inst_RemoveUserData "अपनी OpenSense सेटिंग्स और लॉग भी हटाएँ?"
; Uninstall message.
${LangFileString} Inst_RebootToFinish "कुछ OpenSense फ़ाइलें उपयोग में हैं और Windows के पुनः प्रारंभ होने पर हटा दी जाएँगी।"
