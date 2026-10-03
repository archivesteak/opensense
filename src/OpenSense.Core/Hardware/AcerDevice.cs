using OpenSense.Core.Hardware.Hid;
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

    /// <summary>The embedded controller's HID interface (2024+ Predators); null on laptops without one.</summary>
    public EcHidDevice? EcHid { get; init; }

    /// <summary>What the model's reviewed BIOS is known to do (<see cref="AcerFirmwareProfile"/>); the generic one elsewhere.</summary>
    public AcerFirmwareProfile FirmwareProfile { get; init; } = new();

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

    /// <summary>
    /// The boost last set for the fan, whatever its behaviour now (see <see cref="FanBoostValue"/>). Null without asking
    /// where the firmware's getter doesn't echo the setter (<see cref="AcerFirmwareProfile.FanBoostReadbackReliable"/>).
    /// </summary>
    public int? GetFanBoost(FanChannel fan) =>
        FirmwareProfile.FanBoostReadbackReliable
        && Get(GamingClass, "GetGamingFanSpeed", FanBoostQuery(fan)) is { } output && FanBoostValue(output) is <= 100 and var b ? b : null;

    // --- fan table -------------------------------------------------------------------------

    /// <summary>The raw <c>GetGamingFanTable</c> answer (it takes no input), or null when the firmware lacks it.</summary>
    public ulong? GetFanTableRaw() =>
        Call(GamingClass, "GetGamingFanTable") is { } outputs && outputs.Value("gmOutput") is { } output ? output : null;

    /// <summary>The embedded controller's fan curve; null when none has been picked, or on an error.</summary>
    public FanTable? GetFanTable() => GetFanTableRaw() is { } output && IsOk(output) ? FanTableValue(output) : null;

    public bool SetFanTable(FanTable table) => Set(GamingClass, "SetGamingFanTable", FanTableInput(table));

    // --- CoolBoost -------------------------------------------------------------------------

    public bool? GetCoolBoost() =>
        Get(ActionClass, "GetFunction", CoolBoostGetInput) is { } output ? CoolBoostValue(output) : null;

    public bool SetCoolBoost(bool on) => Set(ActionClass, "SetFunction", CoolBoostSetInput(on));

    // --- Dust Defender ---------------------------------------------------------------------

    /// <summary>Whether a cleaning run is going; null when the firmware doesn't answer.</summary>
    public bool? GetDustDefenderRunning() =>
        Get(ActionClass, "GetFunction", DustDefenderStatusQuery) is { } output ? DustDefenderValue(output) : null;

    public DustDefenderStart StartDustDefender() => Raw(ActionClass, "SetFunction", DustDefenderStartInput) switch
    {
        { } output when IsOk(output) => DustDefenderStart.Started,
        { } output when Status(output) == DustDefenderBusy => DustDefenderStart.Busy,
        _ => DustDefenderStart.Failed,
    };

    // --- battery and USB charging ----------------------------------------------------------

    public BatteryHealthStatus? GetBatteryHealth() =>
        Call(BatteryClass, "GetBatteryHealthControlStatus", BatteryProtocol.StatusArguments()) is { } outputs
            ? BatteryProtocol.DecodeStatus(outputs)
            : null;

    public bool SetBatteryFunction(BatteryFunction function, bool on) =>
        Call(BatteryClass, "SetBatteryHealthControl", BatteryProtocol.SetArguments(function, on)) is { } outputs
        && BatteryProtocol.SetSucceeded(outputs);

    /// <summary>The firmware's battery-boost flag (see <see cref="BatteryBoostValue"/>).</summary>
    public bool? GetBatteryBoost() =>
        Get(GamingClass, "GetGamingSysInfo", BatteryStatusQuery) is { } output ? BatteryBoostValue(output) : null;

    public UsbChargingState? GetUsbCharging() =>
        Get(ActionClass, "GetFunction", UsbChargingQuery) is { } output ? UsbChargingValue(output) : null;

    public bool SetUsbCharging(bool on, int floor) =>
        UsbChargingFloors.Contains(floor) && Set(ActionClass, "SetFunction", UsbChargingInput(on, floor));

    // --- operating mode / GPU mode ---------------------------------------------------------

    /// <summary>Null without asking where the reviewed BIOS has no WMI operating modes.</summary>
    public OperatingMode? GetOperatingMode() =>
        FirmwareProfile.WmiOperatingModesSupported != false
        && GetMisc(MiscSetting.OperatingMode) is { } v && Enum.IsDefined((OperatingMode)v) ? (OperatingMode)v : null;

    /// <summary>
    /// Where the firmware moves the mode relative to the one in force, goes through Balanced first. The AN515-57 (V1.17)
    /// moves the Intel DTT profile index from where it is (Quiet one down, Performance one up, Turbo two up) and only
    /// Balanced sets it, so writing a mode again, or going from one of those modes straight to another, would leave DTT
    /// on another mode's power limits; a failed reset leaves the target unsent. Firmware that sets the mode itself
    /// (<see cref="AcerFirmwareProfile.RequiresBalancedModeReset"/> off) gets the mode alone; firmware without WMI modes
    /// gets nothing.
    /// </summary>
    public bool SetOperatingMode(OperatingMode mode)
    {
        if (FirmwareProfile.WmiOperatingModesSupported == false)
            return false;
        if (FirmwareProfile.RequiresBalancedModeReset && mode != OperatingMode.Balanced
            && !SetMisc(MiscSetting.OperatingMode, (byte)OperatingMode.Balanced))
            return false;
        return SetMisc(MiscSetting.OperatingMode, (byte)mode);
    }

    /// <summary>
    /// The WMI copy of a mode the embedded controller's HID interface has just set, as Acer's software writes it there:
    /// once, whatever the firmware does with it. The controller's answer is what counts, so the result isn't checked.
    /// </summary>
    public void SynchronizeHidOperatingMode(OperatingMode mode)
    {
        if (FirmwareProfile.WmiOperatingModesSupported == false || !FirmwareProfile.RequiresBalancedModeReset)
            SetMisc(MiscSetting.OperatingMode, (byte)mode);
        else
            SetOperatingMode(mode);
    }

    /// <summary>The mode where it is <c>APGeAction</c> function 7's (<see cref="DeviceCapabilities.ActionOperatingModes"/>).</summary>
    public OperatingMode? GetActionOperatingMode() =>
        Get(ActionClass, "GetFunction", ActionModeQuery) is { } output ? ActionModeValue(output) : null;

    public bool SetActionOperatingMode(OperatingMode mode) =>
        ActionModeInput(mode) is { } input && Set(ActionClass, "SetFunction", input);

    public GpuMode? GetGpuMode() =>
        GetMisc(MiscSetting.GpuMode) is { } v && Enum.IsDefined((GpuMode)v) ? (GpuMode)v : null;

    /// <summary>
    /// Takes effect after a reboot. The ANV16-41/42/61 BIOSes answer status 8 for a mode they stored (as the BIOS settings
    /// interface says "stored, applies at the next start"), so that counts as done too.
    /// </summary>
    public bool SetGpuMode(GpuMode mode) =>
        Raw(GamingClass, "SetGamingMiscSetting", MiscSetInput(MiscSetting.GpuMode, (byte)mode)) is { } output && Status(output) is 0 or 8;

    /// <summary>The animation and sound at power-on; null when the firmware answers anything but 0 or 1.</summary>
    public bool? GetBootAnimation() => GetMisc(MiscSetting.BootAnimation) switch
    {
        0 => false,
        1 => true,
        _ => null,
    };

    /// <summary>Takes effect the next time the laptop starts.</summary>
    public bool SetBootAnimation(bool on) => SetMisc(MiscSetting.BootAnimation, on ? (byte)1 : (byte)0);

    /// <summary>Whether the firmware shows the custom boot logo; null when it answers anything but 0 or 1.</summary>
    public bool? GetCustomBootLogo() => GetMisc(MiscSetting.CustomBootLogo) switch
    {
        0 => false,
        1 => true,
        _ => null,
    };

    public bool SetCustomBootLogo(bool on) => SetMisc(MiscSetting.CustomBootLogo, on ? (byte)1 : (byte)0);

    /// <summary>Fn lock (misc 0x0F): 1 locked, 2 unlocked; null for any other answer or a failed call.</summary>
    public bool? GetFnLock() => GetMisc(MiscSetting.FnLock) switch
    {
        1 => true,
        2 => false,
        _ => null,
    };

    public bool SetFnLock(bool locked) => SetMisc(MiscSetting.FnLock, locked ? (byte)1 : (byte)2);

    public byte? GetMisc(MiscSetting setting) =>
        Get(GamingClass, "GetGamingMiscSetting", MiscGetInput(setting)) is { } output ? MiscValue(output) : null;

    public bool SetMisc(MiscSetting setting, byte value) =>
        Set(GamingClass, "SetGamingMiscSetting", MiscSetInput(setting, value));

    // --- keyboard lighting -----------------------------------------------------------------

    /// <summary>Firmware settle time Acer's service waits after lighting writes.</summary>
    private static readonly TimeSpan LightingSettle = TimeSpan.FromMilliseconds(30);

    /// <param name="payload">The 16-byte record (<see cref="KeyboardProtocol.BacklightPayload"/>).</param>
    public bool SetKeyboardBacklight(byte[] payload)
    {
        var ok = SetArray(GamingClass, "SetGamingKBBacklight", payload);
        Thread.Sleep(LightingSettle);
        return ok;
    }

    /// <summary>
    /// Current backlight record (16 bytes) or null when the firmware has no RGB backlight. Where the method takes an
    /// input it gets 1, as PredatorSense passes: the PHN16-73 (BIOS 1.28) refuses anything else. The AN515-57's takes none.
    /// </summary>
    public byte[]? GetKeyboardBacklight()
    {
        try
        {
            try
            {
                var outputs = Transport.InvokeNamed(GamingClass, "GetGamingKBBacklight", [new WmiArgument("gmInput", 1u)]);
                if (outputs.Bytes("gmOutput") is { } answer)
                    return IsOk(outputs.Value("gmReturn") ?? 0) && answer.Length >= 8 ? answer : null;
            }
            catch (AcerWmiException ex) when (ex is not AcerWmiAccessDeniedException)
            {
                // Declared without an input: called as such below.
            }
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

    /// <summary>
    /// The brightness alone, sent back in the record the firmware answered so effect, colour and status stay as they are
    /// (the keyboards whose firmware only acts on the brightness). Byte 3 goes back as 0, as the setters send it.
    /// </summary>
    /// <param name="record">What <see cref="GetKeyboardBacklight"/> answered.</param>
    public bool SetKeyboardBrightness(int brightness, IReadOnlyList<byte> record)
    {
        if (record.Count < 9)
            return false;
        var payload = new byte[16];
        for (var i = 0; i < 9; i++)
            payload[i] = record[i];
        payload[2] = (byte)Math.Clamp(brightness, 0, 100);
        if (payload[3] == 0xFF)
            payload[3] = 0;
        payload[9] = 1;
        var ok = SetArray(GamingClass, "SetGamingKBBacklight", payload);
        Thread.Sleep(LightingSettle);
        return ok;
    }

    public bool SetZoneColor(int zone, RgbColor color) =>
        Set(GamingClass, "SetGamingRgbKb", KeyboardProtocol.ZoneColorInput(zone, color));

    /// <summary>A zone's colour as the firmware keeps it (<c>GetGamingRgbKb</c>), or null.</summary>
    public RgbColor? GetZoneColor(int zone) =>
        Get(GamingClass, "GetGamingRgbKb", KeyboardProtocol.ZoneColorQuery(zone)) is { } output ? KeyboardProtocol.ZoneColorValue(output) : null;

    /// <summary>
    /// Which of the first <paramref name="zones"/> zones are lit (<c>GetGamingLED(0x08)</c>), or null. The answer is the
    /// setter's u64 (status in its low byte, zone n at bit 40 + n − 1), or the same bytes as an array: with the status
    /// in byte 0, or (newer firmware) in a separate <c>gmReturn</c> with the array starting one byte later.
    /// </summary>
    public IReadOnlyList<bool>? GetZonesEnabled(int zones)
    {
        if (Call(GamingClass, "GetGamingLED", new WmiArgument("gmInput", KeyboardProtocol.ZoneEnableQuery)) is not { } outputs)
            return null;
        if (outputs.Bytes("gmOutput") is { } bytes)
        {
            var separate = outputs.Value("gmReturn");
            var maskAt = separate is null ? 5 : 4;
            var status = separate ?? (bytes.Length > 0 ? bytes[0] : 1UL);
            if (!IsOk(status) || bytes.Length <= maskAt)
                return null;
            return KeyboardProtocol.ZoneEnableValue((ulong)bytes[maskAt] << 40, zones);
        }
        return outputs.Value("gmOutput") is { } output && IsOk(output) ? KeyboardProtocol.ZoneEnableValue(output, zones) : null;
    }

    /// <param name="length">What the firmware takes (<see cref="AcerSmbios.LedArrayLength"/>): 8 is the plain integer.</param>
    public bool SetZonesEnabled(IReadOnlyList<bool> zonesOn, int length)
    {
        var input = KeyboardProtocol.ZoneEnableInput(zonesOn);
        var ok = length > 8
            ? SetArray(GamingClass, "SetGamingLED", KeyboardProtocol.ZoneEnableArray(input, length))
            : Set(GamingClass, "SetGamingLED", input);
        Thread.Sleep(LightingSettle);
        return ok;
    }

    // --- light bars and logo ---------------------------------------------------------------

    /// <summary>
    /// The light bars <c>GetGamingLED(0x10)</c> reports, or null when it answers no layout: firmware without light bars
    /// declares the output as an integer (AN515-57: status 2).
    /// </summary>
    /// <param name="wide">Gaming interface 2.91 on: two bytes per bar.</param>
    public IReadOnlyList<LightBar>? GetLightBars(bool wide) =>
        LightBarLayout() is { Status: 0, Data: { } data } ? LightBarProtocol.DecodeLayout(data, wide) : null;

    /// <summary>The raw <c>GetGamingLED(0x10)</c> answer, for diagnostics.</summary>
    public (ulong Status, byte[]? Data)? LightBarLayout()
    {
        if (Call(GamingClass, "GetGamingLED", new WmiArgument("gmInput", LightBarProtocol.LayoutQuery)) is not { } outputs)
            return null;
        var data = outputs.Bytes("gmOutput");
        var status = data is null ? outputs.Value("gmOutput") ?? 1 : outputs.Value("gmReturn") ?? 0;
        return (status & 0xFF, data);
    }

    /// <summary>Switches a bar's zones on and off: the array form from interface 2.86, else Acer's fixed value.</summary>
    public bool SetLightBarZones(LightBarId bar, IReadOnlyList<bool> zonesOn, int length)
    {
        var ok = length > 8
            ? SetArray(GamingClass, "SetGamingLED", LightBarProtocol.OnOffArray(bar, zonesOn, length))
            : Set(GamingClass, "SetGamingLED", LightBarProtocol.LegacyOnOff);
        Thread.Sleep(LightingSettle);
        return ok;
    }

    public bool SetLightBarZoneColor(LightBarId bar, int zone, RgbColor color)
    {
        var ok = Set(GamingClass, "SetGamingRgbKb", LightBarProtocol.ZoneColorInput(bar, zone, color));
        Thread.Sleep(LightingSettle);
        return ok;
    }

    /// <summary>An effect (or, for static, the brightness) on every light bar.</summary>
    public bool SetLightBarBacklight(LightBarEffect effect, int speed, int brightness, byte direction, RgbColor color)
    {
        var ok = SetArray(GamingClass, "SetGamingKBBacklight", LightBarProtocol.BacklightPayload(effect, speed, brightness, direction, color));
        Thread.Sleep(LightingSettle);
        return ok;
    }

    /// <summary>The lid logo is there: <c>GetGamingLEDBehavior(1)</c> answers with status 0 (AN515-57: 1).</summary>
    public bool HasLogo() => Raw(GamingClass, "GetGamingLEDBehavior", LogoProtocol.Group) is { } output && IsOk(output);

    /// <summary>The logo's colour, brightness and speed, then its behaviour, in that order as Acer's software sends them.</summary>
    public bool SetLogo(LogoBehavior behavior, RgbColor color, int brightness, int speed)
    {
        var ok = Set(GamingClass, "SetGamingLEDColor", LogoProtocol.ColorInput(color, brightness, speed));
        Thread.Sleep(LightingSettle);
        ok &= Set(GamingClass, "SetGamingLEDBehavior", LogoProtocol.BehaviorInput(behavior));
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

    /// <summary>(brightness %, auto-off seconds) through the interface the keyboard uses for it, or null.</summary>
    public (int Brightness, int TimeoutSeconds)? GetBacklightTimeout(KeyboardCapabilities keyboard) =>
        keyboard.EcHidBacklightTimeout ? EcHid?.ReadBacklightTimeout()
        : keyboard.BacklightHotkey is { } hotkey ? GetBacklightTimeout(hotkey)
        : null;

    public bool SetBacklightTimeout(KeyboardCapabilities keyboard, int brightness, int timeoutSeconds) =>
        keyboard.EcHidBacklightTimeout ? EcHid?.WriteBacklightTimeout(brightness, timeoutSeconds) == true
        : keyboard.BacklightHotkey is { } hotkey && SetBacklightTimeout(hotkey, brightness, timeoutSeconds);

    public ulong? GetGamingProfile() => Get(GamingClass, "GetGamingProfile", KeyboardProtocol.ProfileQuery);

    public bool SetWindowsKeyEnabled(bool enabled) => Set(GamingClass, "SetGamingProfile", KeyboardProtocol.WindowsKeyInput(enabled));

    public bool SetLcdOverdrive(bool on) => Set(GamingClass, "SetGamingProfile", KeyboardProtocol.LcdOverdriveInput(on));

    // --- BIOS settings ---------------------------------------------------------------------

    /// <summary>The BIOS offers its settings to Windows (the classes exist; whether it lists any is <see cref="GetBiosSettings"/>'s to tell).</summary>
    public bool HasBiosSettings() =>
        Transport.IsClassAvailable(BiosProtocol.MethodClass) && Transport.IsClassAvailable(BiosProtocol.ListClass);

    /// <summary>The settings the BIOS lists with their values now; empty when it lists none (its own switch for this is off).</summary>
    public IReadOnlyList<BiosSetting> GetBiosSettings()
    {
        try
        {
            return BiosProtocol.ParseList(Transport.ReadStrings(BiosProtocol.ListClass, BiosProtocol.ListProperty));
        }
        catch (AcerWmiAccessDeniedException)
        {
            throw;
        }
        catch (AcerWmiException)
        {
            return [];
        }
    }

    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/> (a password setting takes the new password). The BIOS keeps it for the next start.</summary>
    public BiosChangeResult SetBiosSetting(string name, string value, string password) =>
        BiosCall(BiosProtocol.SetMethod, new WmiArgument("Password", password), new WmiArgument("BiosItem", name),
            new WmiArgument("BiosNewSetting", value));

    public BiosChangeResult LoadBiosDefaults(string password) =>
        BiosCall(BiosProtocol.LoadDefaultsMethod, new WmiArgument("Password", password));

    /// <summary>Loads the defaults the user saved in the BIOS setup; the BIOS says unsupported when it has none.</summary>
    public BiosChangeResult LoadBiosUserDefaults(string password) =>
        BiosCall(BiosProtocol.LoadUserDefaultsMethod, new WmiArgument("Password", password));

    private BiosChangeResult BiosCall(string method, params WmiArgument[] inputs) =>
        Call(BiosProtocol.MethodClass, method, inputs) is { } outputs ? BiosProtocol.Result(outputs.Text("Return")) : BiosChangeResult.Failed;

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

    /// <summary>Outputs of a call with named parameters, or <c>null</c> if the call threw. Statuses are not checked.</summary>
    public WmiOutputs? Call(string className, string method, params WmiArgument[] inputs)
    {
        try
        {
            return Transport.InvokeNamed(className, method, inputs);
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
