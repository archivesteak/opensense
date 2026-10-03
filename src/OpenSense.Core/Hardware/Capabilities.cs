using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Lighting;
using static OpenSense.Core.Hardware.AcerProtocol;

namespace OpenSense.Core.Hardware;

public sealed record KeyboardCapabilities
{
    public static KeyboardCapabilities None { get; } = new();

    /// <summary>Zoned RGB backlight with effects.</summary>
    public bool RgbBacklight { get; init; }

    public int Zones { get; init; }

    /// <summary>The effects offered besides static colours.</summary>
    public IReadOnlyList<KeyboardEffect> Effects { get; init; } = [];

    /// <summary>What <c>SetGamingLED</c> takes on this firmware (<see cref="AcerSmbios.LedArrayLength"/>).</summary>
    public int LedArrayLength { get; init; } = 8;

    /// <summary>How effects go out: as the model's own Acer software sends them (<see cref="KeyboardProtocol.PayloadLayout"/>).</summary>
    public KeyboardPayloadLayout PayloadLayout { get; init; }

    /// <summary>Keyboard-backlight hotkey function number, when backlight auto-off is available through <c>APGeAction</c>.</summary>
    public byte? BacklightHotkey { get; init; }

    /// <summary>Backlight auto-off goes through the embedded controller's HID interface (version 0.6 on), as Acer's software does there.</summary>
    public bool EcHidBacklightTimeout { get; init; }

    /// <summary>Backlight auto-off goes to the USB keyboard itself (<see cref="UsbKeyboardDevice"/>).</summary>
    public bool UsbBacklightTimeout { get; init; }

    public bool WindowsKey { get; init; }

    /// <summary>The Windows key is locked on the USB keyboard itself rather than through the firmware.</summary>
    public bool UsbWindowsKey { get; init; }

    public bool LcdOverdrive { get; init; }

    /// <summary>Fn lock through the firmware (misc 0x0F): where the model's reviewed BIOS has it and it answers.</summary>
    public bool FnLock { get; init; }

    /// <summary>
    /// No RGB through WMI, but the firmware acts on the brightness in the <c>SetGamingKBBacklight</c> record
    /// (<see cref="EcKeyboardControl.BrightnessOnly"/>).
    /// </summary>
    public bool WmiBacklightBrightness { get; init; }

    /// <summary>Zones are switched on and off with <c>SetGamingLED</c>; where the firmware has no switches, a zone that is off goes out black.</summary>
    public bool ZoneSwitches { get; init; } = true;

    /// <summary>The firmware answers which zones are on (<c>GetGamingLED(0x08)</c>); without that a static effect can't be read back.</summary>
    public bool ZoneSwitchReadback { get; init; } = true;

    /// <summary>
    /// The firmware puts its default colours back when every static zone is black, so all black (or all off) goes out
    /// as static at brightness 0 instead, with the zone colours left as they are.
    /// </summary>
    public bool ResetsBlackStaticColors { get; init; }

    /// <summary>Per-channel colour correction Acer's software applies for this model (R, G, B).</summary>
    public IReadOnlyList<double> ColorAdjust { get; init; } = [1, 1, 1];

    public IReadOnlyList<RgbColor> DefaultZoneColors { get; init; } = [];

    public bool BacklightAutoOff => EcHidBacklightTimeout || UsbBacklightTimeout || BacklightHotkey is not null;
}

/// <summary>What the battery offers through <c>BatteryControl</c>.</summary>
/// <param name="ChargeLimit">Charging can stop at 80 % (health mode).</param>
public sealed record BatteryCapabilities(bool ChargeLimit, bool Calibration)
{
    public static BatteryCapabilities None { get; } = new(false, false);
}

/// <summary>What the embedded controller's HID interface answers (2024+ Predators, <see cref="EcHidProtocol"/>).</summary>
public sealed record EcHidCapabilities(EcHidVersion Version)
{
    /// <summary>The operating modes it sets, in its own order (<see cref="EcHidProtocol.Modes"/>); empty when it sets none.</summary>
    public IReadOnlyList<OperatingMode> Modes { get; init; } = [];

    /// <summary>It reports whether the battery can help the adapter.</summary>
    public bool BatteryBoost { get; init; }

    /// <summary>It reports the adapter, so a weak USB-C one can hold the modes back (<see cref="Control.PowerLimit.Adapter"/>).</summary>
    public bool Adapter { get; init; }

    /// <summary>How many GPU overclock profiles the firmware has.</summary>
    public int OverclockProfiles { get; init; }

    /// <summary>The GPU overclock the firmware gives each operating mode that has one.</summary>
    public IReadOnlyDictionary<OperatingMode, ClockOffsets> GpuOffsets { get; init; } = new Dictionary<OperatingMode, ClockOffsets>();

    /// <summary>The keyboard backlight's timeout is set here (from version 0.6) rather than through <c>APGeAction</c>.</summary>
    public bool BacklightTimeout { get; init; }
}

public sealed record DeviceCapabilities(
    bool FirmwarePresent,
    IReadOnlyCollection<SensorId> Sensors,
    IReadOnlyList<FanChannel> Fans,
    bool CoolBoost,
    IReadOnlyList<OperatingMode> OperatingModes,
    bool GpuModeSwitch,
    string Diagnostics)
{
    public static DeviceCapabilities None { get; } =
        new(false, new HashSet<SensorId>(), [], false, [], false, "Acer gaming firmware not found.");

    /// <summary>
    /// Modes the firmware says it supports, even when they are not enabled by default
    /// (e.g. models where Acer's own software never exposes them).
    /// </summary>
    public IReadOnlyList<OperatingMode> FirmwareOperatingModes { get; init; } = [];

    /// <summary>
    /// The firmware has CoolBoost, whether or not it is offered (never together with operating modes): it answers, function
    /// 7 doesn't say it is the operating mode instead, and the model's reviewed BIOS has something behind it.
    /// </summary>
    public bool FirmwareCoolBoost { get; init; }

    /// <summary>
    /// The operating modes are <c>APGeAction</c> function 7's (<see cref="AcerProtocol.ActionModes"/>), where its own answer
    /// says it is the operating mode and misc 0x0A/0x0B list none (the NL16-71G).
    /// </summary>
    public bool ActionOperatingModes { get; init; }

    /// <summary>
    /// The model's reviewed BIOS has no WMI operating modes and no EC HID interface sets them: no override turns them on.
    /// </summary>
    public bool OperatingModesUnsupported { get; init; }

    /// <summary>The GPU switch has a third mode, <see cref="GpuMode.Automatic"/> (the BIOS lists it in its answer for misc 9).</summary>
    public bool GpuModeAutomatic { get; init; }

    public KeyboardCapabilities Keyboard { get; init; } = KeyboardCapabilities.None;

    /// <summary>The lights OpenSense can drive: the keyboard backlight, light bars, logos, HID and USB lights.</summary>
    public IReadOnlyList<LightingDeviceInfo> Lights { get; init; } = [];

    /// <summary>
    /// The MagForce keys' light a Sunrex keyboard can have (null without one); in <see cref="Lights"/> on the models
    /// Acer's software names, or when the user turns it on (<see cref="CapabilityOverrides.MagKey"/>).
    /// </summary>
    public LightingDeviceInfo? MagKeyLight { get; init; }

    /// <summary>The light bars on the embedded controller, front to rear (one light: effects reach all of them).</summary>
    public IReadOnlyList<LightBar> LightBars { get; init; } = [];

    public BatteryCapabilities Battery { get; init; } = BatteryCapabilities.None;

    /// <summary>Devices can charge from USB while the laptop is off.</summary>
    public bool UsbCharging { get; init; }

    /// <summary>The firmware keeps a boot animation and sound setting (whether the model has either can't be told).</summary>
    public bool BootAnimation { get; init; }

    /// <summary>
    /// The BIOS has Acer's settings interface (<see cref="BiosProtocol"/>: the 2025 Predators on). Whether it lists any
    /// settings is only known when they are read: its switch for that is set at boot.
    /// </summary>
    public bool BiosSettings { get; init; }

    /// <summary>
    /// The longest new BIOS password the BIOS keeps whole: 16 characters unless the model's reviewed BIOS takes more
    /// (<see cref="AcerFirmwareProfile.BiosPasswordMaxLength"/>).
    /// </summary>
    public int BiosPasswordMaxLength { get; init; } = 16;

    /// <summary>
    /// The firmware shows a picture from the EFI system partition at power-on (SMBIOS record 0x0D or
    /// <see cref="CustomBootLogoSwitch"/>, and an EFI system partition).
    /// </summary>
    public bool CustomBootLogo { get; init; }

    /// <summary>The firmware has the BIOS setup's switch for the custom boot logo (misc 8, "Customize POST animation").</summary>
    public bool CustomBootLogoSwitch { get; init; }

    /// <summary>The firmware can run the fans backwards for a moment to blow dust out (Acer's DustDefender).</summary>
    public bool DustDefender { get; init; }

    /// <summary>
    /// The embedded controller has fan curves to choose from (<see cref="Hardware.FanTable"/>): on the models Acer's
    /// software sets them on (<see cref="AcerProtocol.UsesFanTable"/>).
    /// </summary>
    public bool FanTable { get; init; }

    /// <summary>
    /// The firmware answers whether the battery can help the adapter (<c>GetGamingSysInfo(0x02)</c>, or the EC HID
    /// interface). With that off the performance modes wait (see <see cref="Control.PowerLimit.LowBattery"/>), once the
    /// flag has been seen on: the NL16-71G answers a constant 0.
    /// </summary>
    public bool BatteryBoostFlag { get; init; }

    /// <summary>The laptop has a Mode key: nothing tells before it is pressed once, which state.json then remembers.</summary>
    public bool ModeKey { get; init; }

    /// <summary>The embedded controller's HID interface (2024+ Predators); null without one.</summary>
    public EcHidCapabilities? EcHid { get; init; }

    public AcerSmbios Smbios { get; init; } = AcerSmbios.Empty;

    /// <summary>
    /// Whose rules the operating modes follow: the embedded controller's where its HID interface sets them and has
    /// GPU overclock profiles (the models Acer's software hands to Quick Access), else Acer's gaming software's.
    /// </summary>
    public ModeRules ModeRules => HasOperatingModes && EcHid is { Modes.Count: > 0, OverclockProfiles: > 0 }
        ? ModeRules.EmbeddedController
        : ModeRules.Gaming;

    /// <summary>The fans OpenSense can drive (the others only report their speed).</summary>
    [JsonIgnore]
    public IReadOnlyList<FanChannel> ControllableFans => [.. Fans.Where(f => f.Controllable)];

    public bool HasOperatingModes => OperatingModes.Count > 0;

    /// <summary>Windows power plans are offered where operating modes are not (as NitroSense does).</summary>
    public bool PowerPlans => !HasOperatingModes;

    public bool Has(SensorId sensor) => Sensors.Contains(sensor);
}

/// <summary>User overrides for detected capabilities, the Settings page's switches (null = use detection).</summary>
public sealed record CapabilityOverrides
{
    public bool? OperatingModes { get; init; }

    /// <summary>The MagForce keys' light on a Sunrex keyboard, whatever the model name says.</summary>
    public bool? MagKey { get; init; }

    public DeviceCapabilities Apply(DeviceCapabilities caps)
    {
        var lights = caps.Lights;
        if (caps.MagKeyLight is { } mag)
        {
            var listed = lights.Any(l => l.Source == mag.Source);
            if (MagKey == true && !listed)
                lights = [.. lights, mag];
            else if (MagKey == false && listed)
                lights = [.. lights.Where(l => l.Source != mag.Source)];
        }

        IReadOnlyList<OperatingMode> modes = OperatingModes switch
        {
            true when !caps.HasOperatingModes => caps.FirmwareOperatingModes.Count > 0 ? caps.FirmwareOperatingModes
                : caps.OperatingModesUnsupported ? []
                : CapabilityProbe.DefaultOperatingModes,
            false => [],
            _ => caps.OperatingModes,
        };
        return caps with
        {
            OperatingModes = modes,
            // As NitroSense does: CoolBoost where the firmware has it and operating modes are off.
            CoolBoost = OperatingModes is null ? caps.CoolBoost : modes.Count == 0 && caps.FirmwareCoolBoost,
            Lights = lights,
        };
    }
}

public static class CapabilityProbe
{
    public static IReadOnlyList<OperatingMode> DefaultOperatingModes { get; } =
        [OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance];

    /// <summary>
    /// What the laptop has, from the firmware's own answers, Acer's SMBIOS structures, the built-in model catalog
    /// (<see cref="AcerModelProfile"/>) and what the model's reviewed BIOS is known to do (<see cref="AcerDevice.FirmwareProfile"/>).
    /// Nothing written; nothing read from Acer's software.
    /// </summary>
    /// <param name="model">The laptop's model name, for the features Acer's software offers by model.</param>
    public static DeviceCapabilities Probe(AcerDevice device, AcerSmbios? smbios = null, string? model = null)
    {
        if (!device.IsPresent)
            return DeviceCapabilities.None;

        smbios ??= AcerSmbios.Empty;
        var catalog = AcerModelProfile.For(model);
        var profile = device.FirmwareProfile;
        var diag = new StringBuilder();
        void Log(string text) => diag.AppendLine(text);

        Log($"OpenSense {typeof(CapabilityProbe).Assembly.GetName().Version} capability probe");
        Log($"Model catalog: {catalog.ModelCode ?? "not listed"}; firmware profile: {profile.ModelCode ?? "generic"}");
        Log($"SMBIOS 0xAC gaming interface: {smbios.GamingVersion?.ToString("0.00", CultureInfo.InvariantCulture) ?? "absent"}; records: " +
            string.Join(" ", smbios.GamingRecords.Select(r => $"{r.Id:X2}={r.Value:X}")));
        Log("SMBIOS 0xAA hotkey functions (id/flag=value): " + string.Join(" ", smbios.HotkeyFunctions.Select(r => $"{r.Id:X2}/{r.Flag:X2}={r.Value:X}")));
        EcHidCapabilities? ecHid;
        try
        {
            ecHid = ProbeEcHid(device.EcHid, Log);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The interface is optional: what WMI has stays.
            Log($"EC HID capability probe failed: {ex.GetType().Name}: {ex.Message}");
            ecHid = null;
        }

        // Sensors: every read answers "ok" (absent sensors read 0), so the supported-sensor bitmap decides and only its
        // sensors are read. Without one, every sensor is read and those above 0 count (older firmware); where that finds
        // no fan, the model's fan counts stand in.
        var maskAnswer = device.Raw(GamingClass, "GetGamingSysInfo", SupportedSensorsQuery);
        Log($"GetGamingSysInfo(0x00) = {Hex(maskAnswer)}");
        IReadOnlyList<SensorId> listed = maskAnswer is { } ma && IsOk(ma) ? DecodeSensorMask(ma) : [];
        var sensors = new HashSet<SensorId>();
        foreach (var id in listed.Count > 0 ? listed : Enum.GetValues<SensorId>())
        {
            var raw = device.Raw(GamingClass, "GetGamingSysInfo", SensorReadInput(id));
            Log($"sensor {id,-18} raw={Hex(raw)}");
            if (listed.Count > 0 || (raw is { } r && IsOk(r) && SensorValue(r) > 0))
                sensors.Add(id);
        }
        if (listed.Count == 0 && !FanChannel.Known.Any(f => sensors.Contains(f.RpmSensor)))
        {
            var known = ModelFanSensors(catalog);
            if (known.Count > 0)
                Log($"sensors: none found; the model's fans ({string.Join(", ", known)})");
            sensors.UnionWith(known);
        }
        if (sensors.Contains(SensorId.GpuFanSpeed))
            sensors.Add(SensorId.GpuTemperature); // reads 0 while the dGPU sleeps, so the fallback above can miss it

        // A fan is there when its RPM sensor is (as Acer's software counts them); system fans only report their speed.
        var fans = FanChannel.Known.Where(f => sensors.Contains(f.RpmSensor)).ToList();

        // APGeAction function 7 is CoolBoost on some firmware and the operating mode on others (where a CoolBoost or Dust
        // Defender write would switch the mode); its sub-function 0 says which. Firmware with neither marker keeps the
        // older rule, a CoolBoost that answers, until a BIOS of its kind is read: older Nitros' answers aren't known.
        var actionClass = device.Transport.IsClassAvailable(ActionClass);
        var functionRaw = actionClass ? device.Raw(ActionClass, "GetFunction", ActionFunctionQuery) : null;
        ActionFunction? actionFunction = functionRaw is { } fr && IsOk(fr) ? ActionFunctionKind(fr) : null;
        Log($"APGeAction function 7 raw={Hex(functionRaw)} ({actionFunction?.ToString() ?? "no answer"})");
        var modeFunction = actionFunction == ActionFunction.OperatingMode;

        // CoolBoost. NitroSense offers it only on models without operating modes. On the reviewed Nitro V 16s the method
        // answers without anything behind it (a stub, or a register the EC never reads).
        var coolRaw = actionClass ? device.Raw(ActionClass, "GetFunction", CoolBoostGetInput) : null;
        Log($"APGeAction CoolBoost raw={Hex(coolRaw)}");
        var coolBoostUnsupported = profile.WmiCoolBoostSupported == false || modeFunction;
        var firmwareCoolBoost = coolRaw is { } c && IsOk(c) && !coolBoostUnsupported;

        // Dust Defender (the same function, sub-function 1): every sub-function answers, byte 3 tells.
        var dustRaw = actionClass ? device.Raw(ActionClass, "GetFunction", DustDefenderQuery) : null;
        var dustStatusRaw = actionClass ? device.Raw(ActionClass, "GetFunction", DustDefenderStatusQuery) : null;
        Log($"APGeAction Dust Defender raw={Hex(dustRaw)} status raw={Hex(dustStatusRaw)}");
        var dustDefender = !modeFunction && dustRaw is { } dust && IsOk(dust) && DustDefenderValue(dust);

        // The embedded controller's fan curves (Get/SetGamingFanTable): where the firmware answers the Get, on the models
        // Acer's software sets them on. The AN515-57's firmware answers, but nothing in it reads the value.
        var fanTableRaw = device.GetFanTableRaw();
        Log($"GetGamingFanTable = {Hex(fanTableRaw)}");
        var fanTable = fanTableRaw is { } ft && IsOk(ft) && UsesFanTable(model);

        // Operating modes. Firmware may list modes on models where Acer's software never uses them (the AN515-57 lists
        // Quiet/Balanced/Performance/Turbo: its firmware hands them to Intel DTT's CPU profiles and NVIDIA's Dynamic
        // Boost, not to the power limit registers). Whether they are offered: the reviewed BIOS, or function 7 saying it
        // is the mode, where either settles it, else the model catalog, else SMBIOS record 0x0F, else not where the
        // firmware has CoolBoost instead. The user can still turn them on, except where the reviewed BIOS has none. Only
        // modes the firmware answers are offered.
        var maskRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.SupportedOperatingModes));
        var modeRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.OperatingMode));
        Log($"misc SupportedOperatingModes raw={Hex(maskRaw)}");
        Log($"misc OperatingMode raw={Hex(modeRaw)}");
        IReadOnlyList<OperatingMode> firmwareModes = [];
        if (profile.WmiOperatingModesSupported == false)
            Log("misc operating modes: none behind them on this model's BIOS");
        else if (maskRaw is { } m && IsOk(m) && DecodeOperatingModeMask(m) is { Count: > 0 } decoded)
            firmwareModes = decoded;
        else if (modeRaw is { } cur && IsOk(cur) && Enum.IsDefined((OperatingMode)MiscValue(cur)))
            firmwareModes = DefaultOperatingModes;
        // Where misc 0x0B has none and function 7 says it is the operating mode (the NL16-71G), its modes are the ones.
        var actionModes = false;
        if (firmwareModes.Count == 0 && modeFunction)
        {
            var actionModeRaw = device.Raw(ActionClass, "GetFunction", ActionModeQuery);
            Log($"APGeAction operating mode raw={Hex(actionModeRaw)}");
            actionModes = actionModeRaw is { } am && IsOk(am) && ActionModeValue(am) is not null;
            if (actionModes)
                firmwareModes = ActionModes;
        }
        var smbiosModes = smbios.Gaming(GamingRecord.OperatingModes);
        // The firmware's own word that function 7 is the mode settles it, as the reviewed BIOS's does for misc 0x0B.
        var modesWanted = profile.WmiOperatingModesSupported == true || actionModes ? true
            : catalog.OperatingModes ?? (smbiosModes is { } sm ? sm == 1 : null);
        var modes = modesWanted switch
        {
            false => [],
            true => firmwareModes,
            null => firmwareCoolBoost ? [] : firmwareModes,
        };
        // Where the embedded controller's HID interface sets the mode, its list is the one Acer's software offers.
        if (ecHid is { Modes.Count: > 0 })
        {
            firmwareModes = modes = ecHid.Modes;
            actionModes = false;
        }
        var modesUnsupported = profile.WmiOperatingModesSupported == false && firmwareModes.Count == 0;

        // GPU MUX switch.
        var gpuSupportRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.GpuModeSupport));
        Log($"misc GpuModeSupport raw={Hex(gpuSupportRaw)}");
        Log($"misc GpuMode raw={Hex(device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.GpuMode)))}");
        // A bit per mode: 1 hybrid, 2 discrete only, 4 automatic selection. The BIOSes seen answer 3 or 7. Without an
        // answer there is no switch: the AN515-45's SMM code writes a misc 2 value into the EC's own flags.
        var gpuModes = gpuSupportRaw is { } g && IsOk(g) ? MiscValue(g) : 0;
        var gpuSwitch = (gpuModes & 3) == 3;
        var gpuAutomatic = gpuSwitch && (gpuModes & 4) != 0;

        var coolBoost = firmwareCoolBoost && modes.Count == 0;

        var keyboard = ProbeKeyboard(device, smbios, catalog, model, actionClass, ecHid?.BacklightTimeout == true, Log);
        List<LightingDeviceInfo> lights = [];
        // Brightness alone: through the backlight record, or the auto-off function's brightness, except where the reviewed
        // BIOS has nothing behind the keyboard's WMI methods (ANV16-72: auto-off works, its brightness byte is ignored).
        if (keyboard.RgbBacklight)
            lights.Add(EcKeyboardBackend.Describe(keyboard));
        else if (keyboard.WmiBacklightBrightness || (profile.KeyboardControl != EcKeyboardControl.Unsupported
                     && (keyboard.BacklightHotkey is not null || keyboard.EcHidBacklightTimeout)))
            lights.Add(EcKeyboardBrightnessBackend.Describe());

        // Light bars where SMBIOS record 0x17 says the embedded controller has them; the lid logo where
        // GetGamingLEDBehavior(1) answers with status 0 (AN515-57: no bars, status 2; no logo, status 1).
        var layout = device.LightBarLayout();
        Log($"GetGamingLED(0x10) = {(layout is { } l ? $"status {l.Status}, " + (l.Data is { } d ? Convert.ToHexString(d) : "no layout") : "error")}");
        IReadOnlyList<LightBar> lightBars = smbios.HasEcLightBars && layout is { Status: 0, Data: { } bytes }
            ? LightBarProtocol.DecodeLayout(bytes, smbios.LedArrayLength >= 16)
            : [];
        if (lightBars.Count > 0)
            lights.Add(EcLightBarBackend.Describe(lightBars, smbios.LedArrayLength));
        var logoRaw = device.Raw(GamingClass, "GetGamingLEDBehavior", LogoProtocol.Group);
        Log($"GetGamingLEDBehavior(0x1) = {Hex(logoRaw)}");
        if (logoRaw is { } logo && IsOk(logo))
            lights.Add(EcLogoBackend.Describe());

        // Battery charge limit and calibration (BatteryControl); power-off USB charging (APGeAction function 4).
        var batteryOutputs = device.Transport.IsClassAvailable(BatteryClass)
            ? device.Call(BatteryClass, "GetBatteryHealthControlStatus", BatteryProtocol.StatusArguments())
            : null;
        Log($"BatteryControl status = {batteryOutputs?.ToString() ?? "error"}");
        var batteryStatus = batteryOutputs is { } bo ? BatteryProtocol.DecodeStatus(bo) : null;
        var battery = new BatteryCapabilities(batteryStatus?.HealthModeSupported == true, batteryStatus?.CalibrationSupported == true);
        var usbRaw = actionClass ? device.Raw(ActionClass, "GetFunction", UsbChargingQuery) : null;
        Log($"APGeAction USB charging raw={Hex(usbRaw)}");
        var usbCharging = usbRaw is { } usb && IsOk(usb);
        var boostRaw = device.Raw(GamingClass, "GetGamingSysInfo", BatteryStatusQuery);
        Log($"GetGamingSysInfo(0x02) = {Hex(boostRaw)}");
        // A flag that answers may still be a constant (the NL16-71G's 0): the control loop lets it hold the modes back
        // only once it has been seen on.
        var wmiBatteryBoost = boostRaw is { } boost && IsOk(boost);
        var batteryBoostFlag = wmiBatteryBoost || ecHid?.BatteryBoost == true;

        // The boot animation and sound (misc 6). Firmware without one answers too (AN515-57: 1), and nothing else
        // tells the models apart, so it is offered wherever the firmware answers, as Acer's software does.
        var bootRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.BootAnimation));
        Log($"GetGamingMiscSetting(0x06) = {Hex(bootRaw)}");
        var bootAnimation = bootRaw is { } br && IsOk(br) && MiscValue(br) is 0 or 1;

        // The BIOS setup's "Customize POST animation" (misc 8), which shows the picture in \EFI\OEM at power-on. Where it
        // answers 0 or 1 the firmware has the custom boot logo, whatever SMBIOS record 0x0D says (AN515-57: 0 there, yet
        // the setting is on and the firmware reads the picture); ids the firmware lacks answer 0xFF.
        var customLogoRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.CustomBootLogo));
        Log($"GetGamingMiscSetting(0x08) = {Hex(customLogoRaw)}");
        var customBootLogoSwitch = customLogoRaw is { } cl && IsOk(cl) && MiscValue(cl) is 0 or 1;

        // The BIOS's own settings: the two classes (MOF version 2.94 on the PHN16-73), not read here.
        var biosSettings = device.HasBiosSettings();
        Log($"BIOS settings classes: {biosSettings}");

        // Extra read-only probes that help map new models.
        foreach (var (method, input) in new (string, uint)[]
                 {
                     ("GetGamingFanBehavior", 0x1), ("GetGamingFanBehavior", 0x8), ("GetGamingFanBehavior", 0x9),
                     ("GetGamingFanBehavior", 0x10), ("GetGamingFanBehavior", 0x19),
                     ("GetGamingFanSpeed", 0x1), ("GetGamingFanSpeed", 0x4), ("GetGamingFanSpeed", 0x5),
                 })
            Log($"{method}(0x{input:X}) = {Hex(device.Raw(GamingClass, method, input))}");

        Log($"=> sensors: {string.Join(", ", sensors)}");
        Log($"=> fans: {string.Join(", ", fans.Select(f => f.Controllable ? f.Name : $"{f.Name} (speed only)"))}; Dust Defender: {dustDefender}; " +
            $"fan table: {fanTable}");
        Log($"=> operating modes: {(modes.Count == 0 ? "none" : string.Join(", ", modes))}" +
            (firmwareModes.Count == 0 ? ""
                : ecHid is { Modes.Count: > 0 } ? " (EC HID)"
                : actionModes ? " (APGeAction function 7)"
                : " (misc 0x0B)") +
            (firmwareModes.Count > 0 && modes.Count == 0 ? $"; firmware lists {string.Join(", ", firmwareModes)}, off by default on this model" : ""));
        Log($"=> CoolBoost: {coolBoost}{(modeFunction ? " (function 7 is the operating mode here)" : coolBoostUnsupported ? " (nothing behind it on this model's BIOS)" : "")}, " +
            $"GPU mode switch: {gpuSwitch}{(gpuAutomatic ? " (with automatic selection)" : "")}");
        Log($"=> keyboard: rgb={keyboard.RgbBacklight} zones={keyboard.Zones} effects={string.Join(",", keyboard.Effects)} " +
            $"ledArray={keyboard.LedArrayLength} layout={keyboard.PayloadLayout} " +
            $"brightness={keyboard.WmiBacklightBrightness} zoneSwitches={keyboard.ZoneSwitches}/{keyboard.ZoneSwitchReadback} " +
            $"autoOff={keyboard.BacklightAutoOff}{(keyboard.BacklightHotkey is { } hk ? $" (0x{hk:X2})" : "")} winKey={keyboard.WindowsKey} " +
            $"overdrive={keyboard.LcdOverdrive} fnLock={keyboard.FnLock}");
        Log($"=> lights: {(lights.Count == 0 ? "none" : string.Join(", ", lights.Select(l => $"{l.Id} ({l.Zones} zones)")))}" +
            $"; light bars: {(lightBars.Count == 0 ? "none" : string.Join(", ", lightBars.Select(b => $"{b.Id} {b.Zones}")))}");
        Log($"=> battery: charge limit={battery.ChargeLimit} calibration={battery.Calibration}; USB charging when off: {usbCharging}; " +
            $"battery-boost flag: {(ecHid?.BatteryBoost == true ? "EC HID" : wmiBatteryBoost ? BatteryBoostValue(boostRaw!.Value) ? "on" : "off" : "absent")}" +
            (batteryBoostFlag ? " (holds the modes back once seen on)" : ""));
        Log(ecHid is null
            ? "=> EC HID: none"
            : $"=> EC HID {ecHid.Version}: modes {(ecHid.Modes.Count == 0 ? "none" : string.Join(", ", ecHid.Modes))}; adapter={ecHid.Adapter}; " +
              $"overclock profiles {ecHid.OverclockProfiles} ({string.Join(", ", ecHid.GpuOffsets.Select(o => $"{o.Key} +{o.Value.CoreMhz}/+{o.Value.MemoryMhz}"))}); " +
              $"backlight timeout={ecHid.BacklightTimeout}");
        Log($"=> startup: boot animation={bootAnimation}, custom boot logo record={smbios.Gaming(GamingRecord.CustomBootLogo)?.ToString(CultureInfo.InvariantCulture) ?? "absent"}, " +
            $"custom boot logo switch={customBootLogoSwitch}");

        return new DeviceCapabilities(true, sensors, fans, coolBoost, modes, gpuSwitch, diag.ToString())
        {
            FirmwareOperatingModes = firmwareModes,
            FirmwareCoolBoost = firmwareCoolBoost,
            ActionOperatingModes = actionModes,
            OperatingModesUnsupported = modesUnsupported,
            GpuModeAutomatic = gpuAutomatic,
            Keyboard = keyboard,
            Lights = lights,
            LightBars = lightBars,
            Battery = battery,
            UsbCharging = usbCharging,
            BootAnimation = bootAnimation,
            BiosSettings = biosSettings,
            BiosPasswordMaxLength = profile.BiosPasswordMaxLength,
            CustomBootLogoSwitch = customBootLogoSwitch,
            DustDefender = dustDefender,
            FanTable = fanTable,
            BatteryBoostFlag = batteryBoostFlag,
            EcHid = ecHid,
            Smbios = smbios,
        };
    }

    /// <summary>
    /// The embedded controller's HID interface: its version, then what it answers. The modes it offers replace the
    /// WMI list where its capability is 3–5 (Balanced alone, 1 or 2, is no choice). Each mode's GPU overclock profile is
    /// read here, once: the firmware's own values don't change.
    /// </summary>
    private static EcHidCapabilities? ProbeEcHid(EcHidDevice? hid, Action<string> log)
    {
        if (hid is null)
            return null;
        log($"EC HID: {hid.Info}");
        if (hid.ReadVersion() is not { } version)
        {
            log("EC HID version: no answer");
            return null;
        }
        log($"EC HID version: {version}");

        ushort? Status(EcHidStatus type)
        {
            var value = hid.ReadStatus(type);
            log($"EC HID status {type} = {(value is { } v ? $"0x{v:X}" : "no answer")}");
            return value;
        }
        var capability = Status(EcHidStatus.ModeCapability);
        IReadOnlyList<OperatingMode> modes = capability is >= 3 and <= 5 ? EcHidProtocol.Modes(capability.Value) : [];
        var boost = Status(EcHidStatus.BatteryBoost);
        var adapter = Status(EcHidStatus.Adapter);
        Status(EcHidStatus.ModeLimit);
        Status(EcHidStatus.UsbCAdapter);
        var profiles = Status(EcHidStatus.OverclockProfiles) is { } count and < 0xFF ? (int)count : 0;

        var offsets = new Dictionary<OperatingMode, ClockOffsets>();
        foreach (var mode in modes)
        {
            if (EcHidProtocol.OverclockProfileFor(modes, mode, profiles) is not { } index)
                continue;
            var profile = hid.ReadOverclockProfile(index);
            log($"EC HID overclock profile {index} ({mode}) = {(profile is { } p ? $"core +{p.CoreMhz} MHz, memory +{p.MemoryMhz} MHz" : "no answer")}");
            if (profile is { IsNone: false })
                offsets[mode] = profile;
        }

        var backlight = version.HasBacklightTimeout ? hid.ReadBacklightTimeout() : null;
        if (version.HasBacklightTimeout)
            log($"EC HID backlight timeout = {(backlight is { } b ? $"{b.Brightness}% / {b.TimeoutSeconds}s" : "no answer")}");

        return new EcHidCapabilities(version)
        {
            Modes = modes,
            BatteryBoost = boost is not null,
            Adapter = adapter is not null,
            OverclockProfiles = profiles,
            GpuOffsets = offsets,
            BacklightTimeout = backlight is not null,
        };
    }

    /// <summary>
    /// The keyboard. RGB: not where the reviewed BIOS has no RGB behind WMI, else SMBIOS record 0x0A (1 single colour,
    /// 2 RGB), else the catalog, else whether the firmware answers the backlight record; SMBIOS record 0x08 of 0 is a
    /// per-key keyboard, which the zoned controls don't reach. Its zones: record 0x08 (1, 3 or 4), else the catalog's, else 4.
    /// </summary>
    /// <param name="ecHidBacklight">The embedded controller's HID interface keeps the backlight timeout (it then replaces <c>APGeAction</c>'s).</param>
    private static KeyboardCapabilities ProbeKeyboard(AcerDevice device, AcerSmbios smbios, AcerModelProfile catalog, string? model,
        bool actionClass, bool ecHidBacklight, Action<string> log)
    {
        var firmware = device.FirmwareProfile;
        var backlight = device.GetKeyboardBacklight();
        log($"GetGamingKBBacklight = {(backlight is null ? "error" : Convert.ToHexString(backlight))}");
        var type = smbios.Gaming(GamingRecord.KeyboardType);
        var rgb = firmware.KeyboardControl switch
        {
            EcKeyboardControl.Unsupported or EcKeyboardControl.BrightnessOnly => false,
            // The ANV16-41's EC drives RGB zones while its BIOS reports a single colour on every unit.
            EcKeyboardControl.Rgb => true,
            _ => type != 0 && smbios.Gaming(GamingRecord.KeyboardColor) switch
            {
                2 => true,
                1 => false,
                _ => catalog.RgbKeyboard ?? backlight is not null,
            },
        };
        var zones = rgb ? type is 1 or 3 or 4 ? type.Value : Math.Clamp(catalog.KeyboardZones ?? 4, 1, 4) : 0;
        var brightness = firmware.KeyboardControl == EcKeyboardControl.BrightnessOnly && backlight is { Length: >= 9 };

        // Backlight auto-off: the hotkey function SMBIOS names for it, else the one measured for the model, as long as
        // the firmware answers it.
        byte? hotkey = null;
        if (!ecHidBacklight && actionClass && (smbios.BacklightHotkey ?? catalog.KeyboardTimeoutHotkey) is { } h)
        {
            var timeout = device.GetBacklightTimeout(h);
            log($"backlight timeout (hotkey 0x{h:X2}) = {(timeout is { } t ? $"{t.Brightness}% / {t.TimeoutSeconds}s" : "error")}");
            if (timeout is not null)
                hotkey = h;
        }

        // Overdrive depends on the panel, so the firmware's answer counts, whatever the model.
        var profile = device.GetGamingProfile();
        log($"GetGamingProfile(0) = {(profile is { } p ? $"0x{p:X}" : "error")}");
        var windowsKey = catalog.WindowsKeyLock != false && profile is { } wp && KeyboardProtocol.WindowsKeyValue(wp) is not null;
        var overdrive = catalog.LcdOverdrive != false && profile is { } op && KeyboardProtocol.LcdOverdriveSupported(op);

        // Which zones are lit, each zone's colour (their raw answers, for keyboards whose zones misbehave), and Fn lock:
        // asked only where they are used.
        var zoneSwitches = firmware.KeyboardZoneSwitches ?? true;
        var switchReadback = rgb && zoneSwitches && device.GetZonesEnabled(zones) is not null;
        if (rgb)
        {
            var switchAnswer = device.Call(GamingClass, "GetGamingLED", new WmiArgument("gmInput", KeyboardProtocol.ZoneEnableQuery));
            log($"GetGamingLED(0x08) = {switchAnswer?.ToString() ?? "error"}");
            log("GetGamingRgbKb = " + string.Join(" ", Enumerable.Range(1, zones)
                .Select(zone => $"{zone}:{Hex(device.Raw(GamingClass, "GetGamingRgbKb", KeyboardProtocol.ZoneColorQuery(zone)))}")));
        }
        bool? fnLock = firmware.SupportsFnLockProbe ? device.GetFnLock() : null;
        if (firmware.SupportsFnLockProbe)
            log($"misc FnLock = {fnLock switch { true => "locked", false => "unlocked", null => "no answer" }}");

        return new KeyboardCapabilities
        {
            RgbBacklight = rgb,
            Zones = zones,
            Effects = rgb ? catalog.KeyboardEffects ?? KeyboardProtocol.ZonedEffects : [],
            LedArrayLength = smbios.LedArrayLength,
            PayloadLayout = KeyboardProtocol.PayloadLayout(model),
            BacklightHotkey = hotkey,
            EcHidBacklightTimeout = ecHidBacklight,
            WindowsKey = windowsKey,
            LcdOverdrive = overdrive,
            FnLock = fnLock is not null,
            WmiBacklightBrightness = brightness,
            ZoneSwitches = zoneSwitches,
            ZoneSwitchReadback = switchReadback,
            ResetsBlackStaticColors = firmware.ResetsBlackStaticColors,
            ColorAdjust = catalog.KeyboardColorScale,
        };
    }

    private static string Hex(ulong? v) => v is { } x ? $"0x{x:X}" : "error";

    /// <summary>The RPM sensors of the fans the catalog gives the model.</summary>
    private static List<SensorId> ModelFanSensors(AcerModelProfile catalog)
    {
        List<SensorId> sensors = [];
        if (catalog.CpuFanCount > 0) sensors.Add(SensorId.CpuFanSpeed);
        if (catalog.GpuFanCount > 0) sensors.Add(SensorId.GpuFanSpeed);
        if (catalog.GpuFanCount > 1) sensors.Add(SensorId.Gpu2FanSpeed);
        if (catalog.SystemFanCount > 0) sensors.Add(SensorId.SystemFanSpeed);
        if (catalog.SystemFanCount > 1) sensors.Add(SensorId.System2FanSpeed);
        return sensors;
    }
}
