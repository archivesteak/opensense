using static OpenSense.Core.Hardware.AcerProtocol;

namespace OpenSense.Core.Hardware;

/// <summary>
/// Typed access to the Acer gaming firmware. Reads return <c>null</c> and writes return <c>false</c>
/// when the firmware rejects a call, so unsupported features degrade instead of throwing.
/// Access-denied is the one error that propagates, because it means the whole app needs elevation.
/// </summary>
public sealed class AcerDevice(IWmiTransport transport)
{
    public IWmiTransport Transport { get; } = transport;

    public bool IsPresent => Transport.IsClassAvailable(GamingClass);

    // --- sensors ---------------------------------------------------------------------------

    public int? ReadSensor(SensorId id) =>
        Get(GamingClass, "GetGamingSysInfo", SensorReadInput(id)) is { } output ? SensorValue(output) : null;

    // --- fans ------------------------------------------------------------------------------

    public bool SetFanBehavior(params (FanChannel Fan, FanBehavior Behavior)[] settings) =>
        settings.Length > 0 && Set(GamingClass, "SetGamingFanBehavior", FanBehaviorInput(settings));

    public bool SetFanSpeed(FanChannel fan, int percent) =>
        Set(GamingClass, "SetGamingFanSpeed", FanSpeedInput(fan, percent));

    public FanBehavior? GetFanBehavior(FanChannel fan) =>
        Get(GamingClass, "GetGamingFanBehavior", FanBehaviorQuery(fan)) is { } output ? FanBehaviorValue(output, fan) : null;

    /// <summary>Duty (%) the firmware is currently driving the fan at, in any mode.</summary>
    public int? GetFanDuty(FanChannel fan) =>
        Get(GamingClass, "GetGamingFanSpeed", fan.SpeedId) is { } output && FanDutyValue(output) is <= 100 and var d ? d : null;

    // --- CoolBoost -------------------------------------------------------------------------

    public bool? GetCoolBoost() =>
        Get(ActionClass, "GetFunction", CoolBoostGetInput) is { } output ? CoolBoostValue(output) : null;

    public bool SetCoolBoost(bool on) => Set(ActionClass, "SetFunction", CoolBoostSetInput(on));

    // --- operating mode / GPU mode ---------------------------------------------------------

    public OperatingMode? GetOperatingMode() =>
        GetMisc(MiscSetting.OperatingMode) is { } v && Enum.IsDefined((OperatingMode)v) ? (OperatingMode)v : null;

    public bool SetOperatingMode(OperatingMode mode) => SetMisc(MiscSetting.OperatingMode, (byte)mode);

    public GpuMode? GetGpuMode() =>
        GetMisc(MiscSetting.GpuMode) is { } v && Enum.IsDefined((GpuMode)v) ? (GpuMode)v : null;

    /// <summary>Takes effect after a reboot.</summary>
    public bool SetGpuMode(GpuMode mode) => SetMisc(MiscSetting.GpuMode, (byte)mode);

    public byte? GetMisc(MiscSetting setting) =>
        Get(GamingClass, "GetGamingMiscSetting", MiscGetInput(setting)) is { } output ? MiscValue(output) : null;

    public bool SetMisc(MiscSetting setting, byte value) =>
        Set(GamingClass, "SetGamingMiscSetting", MiscSetInput(setting, value));

    // --- keyboard lighting -----------------------------------------------------------------

    /// <summary>Firmware settle time Acer's service waits after lighting writes.</summary>
    private static readonly TimeSpan LightingSettle = TimeSpan.FromMilliseconds(30);

    public bool SetKeyboardBacklight(KeyboardEffect effect, int speed, int brightness, KeyboardDirection direction, RgbColor color)
    {
        var ok = SetArray(GamingClass, "SetGamingKBBacklight",
            KeyboardProtocol.BacklightPayload(effect, speed, brightness, direction, color));
        Thread.Sleep(LightingSettle);
        return ok;
    }

    /// <summary>Current backlight record (16 bytes) or null when the firmware has no RGB backlight.</summary>
    public byte[]? GetKeyboardBacklight()
    {
        try
        {
            var result = Transport.InvokeArray(GamingClass, "GetGamingKBBacklight", null);
            return IsOk(result.Status) && result.Data is { Length: >= 8 } data ? data : null;
        }
        catch (AcerWmiAccessDeniedException)
        {
            throw;
        }
        catch (AcerWmiException)
        {
            return null;
        }
    }

    public bool SetZoneColor(int zone, RgbColor color) =>
        Set(GamingClass, "SetGamingRgbKb", KeyboardProtocol.ZoneColorInput(zone, color));

    /// <param name="useArray">Gaming interface ≥ 2.86 (see <see cref="AcerSmbios.UsesArrayLedBehavior"/>).</param>
    public bool SetZonesEnabled(IReadOnlyList<bool> zonesOn, bool useArray)
    {
        var input = KeyboardProtocol.ZoneEnableInput(zonesOn);
        var ok = useArray
            ? SetArray(GamingClass, "SetGamingLED", KeyboardProtocol.ZoneEnableArray(input))
            : Set(GamingClass, "SetGamingLED", input);
        Thread.Sleep(LightingSettle);
        return ok;
    }

    // --- keyboard / display settings ------------------------------------------------------

    /// <summary>(brightness %, auto-off seconds) or null.</summary>
    public (int Brightness, int TimeoutSeconds)? GetBacklightTimeout(byte hotkey) =>
        Get(ActionClass, "GetFunction", KeyboardProtocol.BacklightTimeoutQuery(hotkey)) is { } output
            ? (KeyboardProtocol.BacklightBrightnessValue(output), KeyboardProtocol.BacklightTimeoutValue(output))
            : null;

    public bool SetBacklightTimeout(byte hotkey, int brightness, int timeoutSeconds) =>
        Set(ActionClass, "SetFunction", KeyboardProtocol.BacklightTimeoutInput(hotkey, brightness, timeoutSeconds));

    public ulong? GetGamingProfile() => Get(GamingClass, "GetGamingProfile", KeyboardProtocol.ProfileQuery);

    public bool SetWindowsKeyEnabled(bool enabled) => Set(GamingClass, "SetGamingProfile", KeyboardProtocol.WindowsKeyInput(enabled));

    public bool SetLcdOverdrive(bool on) => Set(GamingClass, "SetGamingProfile", KeyboardProtocol.LcdOverdriveInput(on));

    // --- raw access (diagnostics) ----------------------------------------------------------

    /// <summary>Raw output of a call, or <c>null</c> if the call threw. Status is not checked.</summary>
    public ulong? Raw(string className, string method, ulong input)
    {
        try
        {
            return Transport.Invoke(className, method, input);
        }
        catch (AcerWmiAccessDeniedException)
        {
            throw;
        }
        catch (AcerWmiException)
        {
            return null;
        }
    }

    private ulong? Get(string className, string method, ulong input) =>
        Raw(className, method, input) is { } output && IsOk(output) ? output : null;

    private bool Set(string className, string method, ulong input) =>
        Raw(className, method, input) is { } output && IsOk(output);

    private bool SetArray(string className, string method, byte[] input)
    {
        try
        {
            return IsOk(Transport.InvokeArray(className, method, input).Status);
        }
        catch (AcerWmiAccessDeniedException)
        {
            throw;
        }
        catch (AcerWmiException)
        {
            return false;
        }
    }
}
