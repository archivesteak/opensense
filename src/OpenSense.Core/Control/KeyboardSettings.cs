using System.Text.Json.Serialization;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Control;

/// <summary>
/// Keyboard and display settings OpenSense owns (the backlight's colours are in <see cref="LightingConfig"/>).
/// A null member means "leave the firmware as it is", so a first launch never overwrites what NitroSense configured.
/// </summary>
public sealed record KeyboardSettings
{
    /// <summary>Settings version 1 kept the keyboard's lighting here; read once to move it to <see cref="LightingConfig"/>.</summary>
    [JsonPropertyName("Lighting")]
    public LightingSettings? LegacyLighting { get; init; }

    public bool? BacklightAutoOff { get; init; }

    public bool? WindowsKey { get; init; }

    public bool? LcdOverdrive { get; init; }
}

/// <summary>What the firmware (or the USB keyboard) reports for the keyboard's settings right now.</summary>
public sealed record KeyboardState(bool? BacklightAutoOff, bool? WindowsKey, bool? LcdOverdrive)
{
    /// <param name="usb">The USB keyboard, where it keeps the Windows key or auto-off itself.</param>
    public static KeyboardState Read(AcerDevice device, KeyboardCapabilities caps, UsbKeyboardDevice? usb = null)
    {
        var firmwareWindowsKey = caps.WindowsKey && !caps.UsbWindowsKey;
        var profile = firmwareWindowsKey || caps.LcdOverdrive ? device.GetGamingProfile() : null;
        return new KeyboardState(
            caps.UsbBacklightTimeout ? usb?.ReadAutoOff() : device.GetBacklightTimeout(caps) is { } t ? t.TimeoutSeconds > 0 : null,
            caps.UsbWindowsKey ? usb?.ReadWindowsKeyEnabled()
                : firmwareWindowsKey && profile is { } wp ? KeyboardProtocol.WindowsKeyValue(wp) : null,
            caps.LcdOverdrive && profile is { } op ? KeyboardProtocol.LcdOverdriveValue(op) : null);
    }
}
