using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>The keyboard's brightness, where the firmware has no effective WMI RGB control.</summary>
public sealed class EcKeyboardBrightnessBackend(IDeviceDispatcher dispatcher, KeyboardCapabilities caps)
    : EcLightingBackend(dispatcher, Describe())
{
    public static LightingDeviceInfo Describe() => new(EcKeyboardBackend.Id, LightingLocation.Keyboard, LightingBackendKind.EcKeyboardBrightness)
    {
        Zones = 0,
        Readback = LightingReadbackKind.Brightness,
        BrightnessLevels = KeyboardProtocol.BrightnessLevels,
    };

    protected override bool Apply(AcerDevice device, LightingSettings lighting)
    {
        // Saved RGB settings can outlive a BIOS/backend change. Apply their brightness without inventing RGB support.
        if (caps.WmiBacklightBrightness)
            return device.GetKeyboardBacklight() is { Length: >= 9 } record && device.SetKeyboardBrightness(NearestBrightness(lighting.Brightness), record);
        return device.GetBacklightTimeout(caps) is { } state
            && device.SetBacklightTimeout(caps, NearestBrightness(lighting.Brightness), state.TimeoutSeconds);
    }

    protected override LightingSettings? Read(AcerDevice device)
    {
        int? brightness = caps.WmiBacklightBrightness
            ? device.GetKeyboardBacklight() is { Length: >= 9 } record ? record[2] : null
            : device.GetBacklightTimeout(caps)?.Brightness;
        return brightness is { } level ? new LightingSettings { Brightness = NearestBrightness(level), Zones = [] } : null;
    }
}
