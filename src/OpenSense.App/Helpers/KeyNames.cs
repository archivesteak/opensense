using OpenSense.Core.Settings;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace OpenSense.App.Helpers;

/// <summary>Key and shortcut names as the current keyboard layout spells them ("Strg + Alt + O" on a German one).</summary>
public static unsafe class KeyNames
{
    public static string Describe(KeyShortcut shortcut)
    {
        var parts = new List<string>();
        if (shortcut.Control)
            parts.Add(Name(VirtualKey.Control));
        if (shortcut.Alt)
            parts.Add(Name(VirtualKey.Menu));
        if (shortcut.Shift)
            parts.Add(Name(VirtualKey.Shift));
        if (shortcut.Windows)
            parts.Add("Win");
        parts.Add(Name((VirtualKey)shortcut.Key));
        return string.Join(" + ", parts);
    }

    public static string Name(VirtualKey key)
    {
        // GetKeyNameText takes the scan code in bits 16-23, and bit 24 for the E0-prefixed (extended) keys.
        var scan = PInvoke.MapVirtualKey((uint)key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
        var lParam = (int)((scan & 0xFF) << 16) | ((scan & 0xFF00) != 0 ? 1 << 24 : 0);
        var buffer = stackalloc char[64];
        var length = scan == 0 ? 0 : PInvoke.GetKeyNameText(lParam, new PWSTR(buffer), 64);
        return length > 0 ? new string(buffer, 0, length) : key.ToString();
    }
}
