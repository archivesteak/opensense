namespace OpenSense.Core.Hardware.Hid;

/// <summary>
/// Which key each LED of a per-key keyboard lights, by product: LED n is the n-th name of the table ("." for none).
/// Key names are <see cref="Lighting.LightingDeviceInfo.Keys"/>'. Chicony keyboards number their LEDs in one order for
/// all three layouts; Sunrex ones by where the key sits in their matrix, with a table per product family.
/// </summary>
public static class UsbKeyboardLeds
{
    /// <summary>The keys and their LEDs; null for a keyboard without a table (it then has effects, no per-key colours).</summary>
    public static IReadOnlyDictionary<string, int>? For(ushort vendorId, ushort productId) =>
        Table(vendorId, productId) is { } table ? Parse(table) : null;

    private static string? Table(ushort vendorId, ushort productId) => (vendorId, productId) switch
    {
        (UsbKeyboardProtocol.ChiconyVendor, 0x0117) => ChiconyUs,
        (UsbKeyboardProtocol.ChiconyVendor, 0x011A) => ChiconyUk,
        (UsbKeyboardProtocol.ChiconyVendor, 0x0119) => ChiconyJapanese,
        (UsbKeyboardProtocol.SunrexVendor, 0x766A or 0x766E) => Sunrex766Uk,
        (UsbKeyboardProtocol.SunrexVendor, 0x766B) => Sunrex766Japanese,
        (UsbKeyboardProtocol.SunrexVendor, 0x766C or 0x766D) => Sunrex766Us,
        (UsbKeyboardProtocol.SunrexVendor, 0x767A or 0x767D) => Sunrex767Us,
        (UsbKeyboardProtocol.SunrexVendor, 0x767B or 0x767E) => Sunrex767Uk,
        (UsbKeyboardProtocol.SunrexVendor, var p) when (p >> 4) is 0x668 or 0x868 && (p & 0xF) is >= 0xA and <= 0xE => Sunrex668,
        (UsbKeyboardProtocol.SunrexVendor, var p) when (p >> 4) is 0x666 or 0x667 or 0x866 or 0x867 => (p & 0xF) switch
        {
            0xA or 0xD => Sunrex666Us,
            0xB or 0xE => Sunrex666Uk,
            0xC => Sunrex666Japanese,
            _ => null,
        },
        _ => null,
    };

    private static Dictionary<string, int> Parse(string table)
    {
        var leds = new Dictionary<string, int>();
        var names = table.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var led = 0; led < names.Length; led++)
        {
            if (names[led] != ".")
                leds.Add(names[led], led); // a key named twice is a mistake in the table
        }
        return leds;
    }

    // Chicony 0117 (US)
    private const string ChiconyUs =
        "ControlLeft ShiftLeft CapsLock Tab Backquote Escape Fn . . . Digit1 F1 MetaLeft Backslash . KeyQ Digit2 " +
        "F2 AltLeft KeyZ KeyA KeyW Digit3 F3 Space KeyX KeyS KeyE Digit4 F4 . KeyC KeyD KeyR Digit5 F5 . KeyV " +
        "KeyF KeyT Digit6 F6 . KeyB KeyG KeyY Digit7 F7 . KeyN KeyH KeyU Digit8 F8 . KeyM KeyJ KeyI Digit9 F9 " +
        "AltRight Comma KeyK KeyO Digit0 F10 . Period KeyL KeyP Minus F11 ContextMenu Slash Semicolon " +
        "BracketLeft Equal F12 ControlRight . Quote BracketRight Backspace PrintScreen ArrowLeft ShiftRight " +
        "Enter . . Insert ArrowUp . . . . Delete ArrowDown Pause Home Numpad7 Numpad1 Numpad4 ArrowRight " +
        "AudioVolumeMute AudioVolumeDown AudioVolumeUp PredatorSense Power Numpad0 Numpad2 Numpad5 Numpad8 " +
        "NumLock PageUp NumpadDecimal Numpad3 Numpad6 Numpad9 NumpadDivide PageDown NumpadEnter . NumpadAdd " +
        "NumpadSubtract NumpadMultiply .";

    // Chicony 0119 (Japanese)
    private const string ChiconyJapanese =
        "ControlLeft ShiftLeft CapsLock Tab Backquote Escape Fn . . . Digit1 F1 MetaLeft IntlYen . KeyQ Digit2 " +
        "F2 AltLeft KeyZ KeyA KeyW Digit3 F3 Space KeyX KeyS KeyE Digit4 F4 . KeyC KeyD KeyR Digit5 F5 . KeyV " +
        "KeyF KeyT Digit6 F6 . KeyB KeyG KeyY Digit7 F7 . KeyN KeyH KeyU Digit8 F8 . KeyM KeyJ KeyI Digit9 F9 " +
        "AltRight Comma KeyK KeyO Digit0 F10 NonConvert Period KeyL KeyP Minus F11 ContextMenu Slash Semicolon " +
        "BracketLeft Equal F12 ControlRight IntlRo Quote BracketRight Backspace PrintScreen ArrowLeft ShiftRight " +
        "Enter IntlHash Convert Insert ArrowUp . . . . Delete ArrowDown Pause Home Numpad7 Numpad1 Numpad4 " +
        "ArrowRight AudioVolumeMute AudioVolumeDown AudioVolumeUp PredatorSense Power Numpad0 Numpad2 Numpad5 " +
        "Numpad8 NumLock PageUp NumpadDecimal Numpad3 Numpad6 Numpad9 NumpadDivide PageDown NumpadEnter . " +
        "NumpadAdd NumpadSubtract NumpadMultiply .";

    // Chicony 011A (UK)
    private const string ChiconyUk =
        "ControlLeft ShiftLeft CapsLock Tab Backquote Escape Fn . . . Digit1 F1 MetaLeft IntlBackslash . KeyQ " +
        "Digit2 F2 AltLeft KeyZ KeyA KeyW Digit3 F3 Space KeyX KeyS KeyE Digit4 F4 . KeyC KeyD KeyR Digit5 F5 . " +
        "KeyV KeyF KeyT Digit6 F6 . KeyB KeyG KeyY Digit7 F7 . KeyN KeyH KeyU Digit8 F8 . KeyM KeyJ KeyI Digit9 " +
        "F9 AltRight Comma KeyK KeyO Digit0 F10 . Period KeyL KeyP Minus F11 ContextMenu Slash Semicolon " +
        "BracketLeft Equal F12 ControlRight . Quote BracketRight Backspace PrintScreen ArrowLeft ShiftRight " +
        "Enter IntlHash . Insert ArrowUp . . . . Delete ArrowDown Pause Home Numpad7 Numpad1 Numpad4 ArrowRight " +
        "AudioVolumeMute AudioVolumeDown AudioVolumeUp PredatorSense Power Numpad0 Numpad2 Numpad5 Numpad8 " +
        "NumLock PageUp NumpadDecimal Numpad3 Numpad6 Numpad9 NumpadDivide PageDown NumpadEnter . NumpadAdd " +
        "NumpadSubtract NumpadMultiply .";

    // Sunrex 666A/D, 667A/D, 866A/D, 867A/D (US)
    private const string Sunrex666Us =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA . Fn F2 Digit2 KeyW KeyS KeyZ " +
        "MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC . F5 Digit5 KeyT KeyG KeyV . F6 " +
        "Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM . F9 Digit9 KeyO KeyL " +
        "Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft Quote Slash . F12 " +
        "Equal BracketRight . . . PrintScreen . . . . ControlRight Insert . . . ShiftRight ArrowLeft Delete " +
        "Backspace Backslash Enter ArrowUp ArrowDown AudioVolumeDown PredatorSense Numpad7 Numpad4 Numpad1 " +
        "ArrowRight AudioVolumeUp NumLock Numpad8 Numpad5 Numpad2 Numpad0 AudioVolumeMute NumpadDivide Numpad9 " +
        "Numpad6 Numpad3 NumpadDecimal Power NumpadMultiply NumpadSubtract NumpadAdd . NumpadEnter . . . . . .";

    // Sunrex 666B/E, 667B/E, 866B/E, 867B/E (UK)
    private const string Sunrex666Uk =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA IntlBackslash Fn F2 Digit2 KeyW " +
        "KeyS KeyZ MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC . F5 Digit5 KeyT KeyG KeyV " +
        ". F6 Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM . F9 Digit9 KeyO " +
        "KeyL Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft Quote Slash . " +
        "F12 Equal BracketRight . . . PrintScreen . . . . ControlRight Insert . . IntlHash ShiftRight ArrowLeft " +
        "Delete Backspace . Enter ArrowUp ArrowDown AudioVolumeDown PredatorSense Numpad7 Numpad4 Numpad1 " +
        "ArrowRight AudioVolumeUp NumLock Numpad8 Numpad5 Numpad2 Numpad0 AudioVolumeMute NumpadDivide Numpad9 " +
        "Numpad6 Numpad3 NumpadDecimal Power NumpadMultiply NumpadSubtract NumpadAdd . NumpadEnter . . . . . .";

    // Sunrex 666C, 667C, 866C, 867C (Japanese; its Convert key, LED 130, is past what the upload carries)
    private const string Sunrex666Japanese =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA . Fn F2 Digit2 KeyW KeyS KeyZ " +
        "MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC NonConvert F5 Digit5 KeyT KeyG KeyV " +
        ". F6 Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM . F9 Digit9 KeyO " +
        "KeyL Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft Quote Slash . " +
        "F12 Equal BracketRight . . . PrintScreen . . . IntlRo ControlRight Insert IntlYen . IntlHash ShiftRight " +
        "ArrowLeft Delete Backspace . Enter ArrowUp ArrowDown AudioVolumeDown PredatorSense Numpad7 Numpad4 " +
        "Numpad1 ArrowRight AudioVolumeUp NumLock Numpad8 Numpad5 Numpad2 Numpad0 AudioVolumeMute NumpadDivide " +
        "Numpad9 Numpad6 Numpad3 NumpadDecimal Power NumpadMultiply NumpadSubtract NumpadAdd . NumpadEnter . . . " +
        ". . .";

    // Sunrex 668A-E, 868A-E (one table for every layout: a LED for each key any of them has)
    private const string Sunrex668 =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA IntlBackslash Fn F2 Digit2 KeyW " +
        "KeyS KeyZ MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC NonConvert F5 Digit5 KeyT " +
        "KeyG KeyV . F6 Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM Convert " +
        "F9 Digit9 KeyO KeyL Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft " +
        "Quote Slash . F12 Equal BracketRight . . . MyKey . . . IntlRo ControlRight PrintScreen IntlYen . " +
        "IntlHash ShiftRight ArrowLeft Delete Backspace Backslash Enter ArrowUp ArrowDown MediaTrackPrevious " +
        "PredatorSense Numpad7 Numpad4 Numpad1 ArrowRight MediaPlayPause NumLock Numpad8 Numpad5 Numpad2 Numpad0 " +
        "MediaTrackNext NumpadDivide Numpad9 Numpad6 Numpad3 NumpadDecimal Power NumpadMultiply NumpadSubtract " +
        "NumpadAdd . NumpadEnter . . . . . .";

    // Sunrex 766A, 766E (UK)
    private const string Sunrex766Uk =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA IntlBackslash Fn F2 Digit2 KeyW " +
        "KeyS KeyZ MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC . F5 Digit5 KeyT KeyG KeyV " +
        ". F6 Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM . F9 Digit9 KeyO " +
        "KeyL Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft Quote Slash . " +
        "F12 Equal BracketRight . . . PrintScreen . . . . ControlRight Insert . . IntlHash ShiftRight ArrowLeft " +
        "Delete Backspace . Enter ArrowUp ArrowDown Power PredatorSense AudioVolumeUp AudioVolumeDown " +
        "AudioVolumeMute ArrowRight . . . . . . . . . . . . . . . . . . . . . . . .";

    // Sunrex 766B (Japanese)
    private const string Sunrex766Japanese =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA . Fn F2 Digit2 KeyW KeyS KeyZ " +
        "MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC NonConvert F5 Digit5 KeyT KeyG KeyV " +
        ". F6 Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM Convert F9 Digit9 " +
        "KeyO KeyL Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft Quote Slash " +
        ". F12 Equal BracketRight . . . PrintScreen . . . IntlRo ControlRight Insert IntlYen . IntlHash " +
        "ShiftRight ArrowLeft Delete Backspace . Enter ArrowUp ArrowDown Power PredatorSense AudioVolumeUp " +
        "AudioVolumeDown AudioVolumeMute ArrowRight . . . . . . . . . . . . . . . . . . . . . . . .";

    // Sunrex 766C, 766D (US)
    private const string Sunrex766Us =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ KeyA . Fn F2 Digit2 KeyW KeyS KeyZ " +
        "MetaLeft F3 Digit3 KeyE KeyD KeyX AltLeft F4 Digit4 KeyR KeyF KeyC . F5 Digit5 KeyT KeyG KeyV . F6 " +
        "Digit6 KeyY KeyH KeyB Space F7 Digit7 KeyU KeyJ KeyN . F8 Digit8 KeyI KeyK KeyM . F9 Digit9 KeyO KeyL " +
        "Comma AltRight F10 Digit0 KeyP Semicolon Period ContextMenu F11 Minus BracketLeft Quote Slash . F12 " +
        "Equal BracketRight . . . PrintScreen . . . . ControlRight Insert . . . ShiftRight ArrowLeft Delete " +
        "Backspace Backslash Enter ArrowUp ArrowDown Power PredatorSense AudioVolumeUp AudioVolumeDown " +
        "AudioVolumeMute ArrowRight . . . . . . . . . . . . . . . . . . . . . . . .";

    // Sunrex 767A, 767D (US)
    private const string Sunrex767Us =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ . . Fn F2 Digit2 . KeyA KeyZ " +
        "MetaLeft F3 Digit3 KeyW KeyS . . F4 Digit4 KeyE KeyD KeyX AltLeft F5 . KeyR KeyF KeyC . F6 Digit5 KeyT " +
        "KeyG KeyV . F7 Digit6 KeyY . KeyB Space F8 Digit7 KeyU KeyH KeyN . F9 Digit8 . KeyJ KeyM . F10 Digit9 " +
        "KeyI KeyK . . F11 Digit0 KeyO KeyL Comma AltRight F12 Minus KeyP Semicolon Period ControlRight " +
        "PredatorSense . BracketLeft Quote Slash ArrowLeft Insert Equal BracketRight . . . Delete . . . ArrowUp " +
        "ArrowDown Power Backspace Backslash Enter ShiftRight ArrowRight . . . . . . . . . . . . . . . . . . . . " +
        ". . . .";

    // Sunrex 767B, 767E (UK)
    private const string Sunrex767Uk =
        "Escape Backquote Tab CapsLock ShiftLeft ControlLeft F1 Digit1 KeyQ . IntlBackslash Fn F2 Digit2 . KeyA " +
        "KeyZ MetaLeft F3 Digit3 KeyW KeyS . . F4 Digit4 KeyE KeyD KeyX AltLeft F5 . KeyR KeyF KeyC . F6 Digit5 " +
        "KeyT KeyG KeyV . F7 Digit6 KeyY . KeyB Space F8 Digit7 KeyU KeyH KeyN . F9 Digit8 . KeyJ KeyM . F10 " +
        "Digit9 KeyI KeyK . . F11 Digit0 KeyO KeyL Comma AltRight F12 Minus KeyP Semicolon Period ControlRight " +
        "PredatorSense . BracketLeft Quote Slash ArrowLeft Insert Equal BracketRight . . . Delete . . IntlHash " +
        "ArrowUp ArrowDown Power Backspace Enter . ShiftRight ArrowRight . . . . . . . . . . . . . . . . . . . . " +
        ". . . .";
}
