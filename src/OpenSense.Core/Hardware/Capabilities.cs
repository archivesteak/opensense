using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Win32;
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

    /// <summary>Per-channel colour correction NitroSense applies for this model (R, G, B).</summary>
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

    /// <summary>The firmware answers CoolBoost, whether or not it is offered (never together with operating modes).</summary>
    public bool FirmwareCoolBoost { get; init; }

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
    /// The firmware reports whether the battery can help the adapter (<c>GetGamingSysInfo(0x02)</c>); with that off, the
    /// performance modes wait (see <see cref="Control.PowerLimit.LowBattery"/>).
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

/// <summary>User overrides for detected capabilities (null = use detection).</summary>
public sealed record CapabilityOverrides
{
    public bool? CoolBoost { get; init; }
    public bool? OperatingModes { get; init; }
    public bool? GpuModeSwitch { get; init; }

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
            true when !caps.HasOperatingModes => caps.FirmwareOperatingModes.Count > 0
                ? caps.FirmwareOperatingModes
                : CapabilityProbe.DefaultOperatingModes,
            false => [],
            _ => caps.OperatingModes,
        };
        return caps with
        {
            OperatingModes = modes,
            // As NitroSense does: CoolBoost where the firmware has it and operating modes are off.
            CoolBoost = CoolBoost ?? (OperatingModes is null ? caps.CoolBoost : modes.Count == 0 && caps.FirmwareCoolBoost),
            GpuModeSwitch = GpuModeSwitch ?? caps.GpuModeSwitch,
            Lights = lights,
        };
    }
}

/// <summary>Values NitroSense's installer leaves on the machine, when present. Used only as hints.</summary>
public sealed record NitroSenseHints
{
    private const string Dir = @"C:\ProgramData\OEM\NitroSense";
    private const string RegistryKey = @"SOFTWARE\OEM\NitroSense";
    private static readonly string[] Channels = ["R", "G", "B"];

    public int? MachineType { get; init; }
    public bool? CpuFan { get; init; }
    public bool? GpuFan { get; init; }
    public bool? SystemFan { get; init; }

    /// <summary>2 = RGB zoned keyboard.</summary>
    public int? KeyboardColor { get; init; }

    public int? KeyboardZones { get; init; }
    public int? BacklightHotkey { get; init; }

    /// <summary>[AdvanceSetting] entries, e.g. "LCD".</summary>
    public IReadOnlyList<string> AdvancedSettings { get; init; } = [];

    /// <summary>[KeyboardSetting] entries, e.g. "Backlight", "Sticky_Key", "Windowskey1".</summary>
    public IReadOnlyList<string> KeyboardSettings { get; init; } = [];

    public IReadOnlyList<double>? ColorAdjust { get; init; }
    public IReadOnlyList<RgbColor> DefaultZoneColors { get; init; } = [];

    public bool Present => MachineType is not null || KeyboardColor is not null;

    public static NitroSenseHints Read()
    {
        var feature = ReadIni(Path.Combine(Dir, "Feature.ini"));
        var hw = ReadIni(Path.Combine(Dir, "HW_Support.ini"));
        using var reg = Registry.LocalMachine.OpenSubKey(RegistryKey);
        using var light = Registry.LocalMachine.OpenSubKey(RegistryKey + @"\LightSetting");

        string?[] adjust = [.. Channels.Select(c => hw.GetValueOrDefault($"ZoneColorAdjust.{c}"))];
        return new NitroSenseHints
        {
            MachineType = Int(feature.GetValueOrDefault("MachineType.Type")),
            CpuFan = Flag(hw, "FanSupport.CPU"),
            GpuFan = Flag(hw, "FanSupport.GPU"),
            SystemFan = Flag(hw, "FanSupport.System"),
            KeyboardColor = light?.GetValue("KeyBoardColor") as int?,
            KeyboardZones = light?.GetValue("KeyBoardArea") as int?,
            BacklightHotkey = reg?.GetValue("BK_Hotkey_Number") as int?,
            AdvancedSettings = List(hw, "AdvanceSetting"),
            KeyboardSettings = List(hw, "KeyboardSetting"),
            ColorAdjust = adjust.All(a => a is not null)
                ? [.. adjust.Select(a => double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1)]
                : null,
            DefaultZoneColors = [.. Enumerable.Range(1, 4)
                .Select(i => hw.GetValueOrDefault($"ZoneDefaultColor.Zone{i}"))
                .OfType<string>()
                .Select(RgbColor.FromHex)],
        };
    }

    private static int? Int(string? value) => int.TryParse(value, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static bool? Flag(Dictionary<string, string> ini, string key) =>
        ini.TryGetValue(key, out var v) ? v.Trim() == "1" : null;

    private static List<string> List(Dictionary<string, string> ini, string section) =>
        [.. ini.Where(kv => kv.Key.StartsWith(section + ".FUN", StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Value)];

    private static Dictionary<string, string> ReadIni(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return result;
        var section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
                section = line[1..^1];
            else if (line.IndexOfAny(['=', ':']) is var i and > 0)
                result[$"{section}.{line[..i].Trim()}"] = line[(i + 1)..].Trim();
        }
        return result;
    }
}

public static class CapabilityProbe
{
    /// <summary>The keyboard-backlight hotkey function on models seen so far (AN515-57).</summary>
    private const byte CommonBacklightHotkey = 0x84;

    public static IReadOnlyList<OperatingMode> DefaultOperatingModes { get; } =
        [OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance];

    /// <param name="model">The laptop's model name, for the features Acer's software offers by model.</param>
    public static DeviceCapabilities Probe(AcerDevice device, NitroSenseHints? hints = null, AcerSmbios? smbios = null, string? model = null)
    {
        if (!device.IsPresent)
            return DeviceCapabilities.None;

        hints ??= new NitroSenseHints();
        smbios ??= AcerSmbios.Empty;
        var diag = new StringBuilder();
        void Log(string text) => diag.AppendLine(text);
        static string Hex(ulong? v) => v is { } x ? $"0x{x:X}" : "error";

        Log($"OpenSense {typeof(CapabilityProbe).Assembly.GetName().Version} capability probe");
        Log(hints.Present
            ? $"NitroSense hints: MachineType={hints.MachineType} fans cpu={hints.CpuFan} gpu={hints.GpuFan} sys={hints.SystemFan} " +
              $"kbColor={hints.KeyboardColor} kbZones={hints.KeyboardZones} bkHotkey={hints.BacklightHotkey} " +
              $"advanced=[{string.Join(",", hints.AdvancedSettings)}] keyboard=[{string.Join(",", hints.KeyboardSettings)}]"
            : "NitroSense hints: none (NitroSense not installed)");
        Log($"SMBIOS 0xAC gaming interface: {smbios.GamingVersion?.ToString("0.00", CultureInfo.InvariantCulture) ?? "absent"}; records: " +
            string.Join(" ", smbios.GamingRecords.Select(r => $"{r.Id:X2}={r.Value:X}")));
        Log("SMBIOS 0xAA hotkey functions: " + string.Join(" ", smbios.HotkeyFunctions.Select(r => $"{r.Id:X2}={r.Value:X}")));
        var ecHid = ProbeEcHid(device.EcHid, Log);

        // Sensors: every read answers "ok" (absent sensors read 0), so use the supported-sensor bitmap.
        var maskAnswer = device.Raw(GamingClass, "GetGamingSysInfo", SupportedSensorsQuery);
        Log($"GetGamingSysInfo(0x00) = {Hex(maskAnswer)}");
        var readings = new Dictionary<SensorId, int?>();
        foreach (var id in Enum.GetValues<SensorId>())
        {
            var raw = device.Raw(GamingClass, "GetGamingSysInfo", SensorReadInput(id));
            readings[id] = raw is { } r && IsOk(r) ? SensorValue(r) : null;
            Log($"sensor {id,-18} raw={Hex(raw)}");
        }
        var sensors = maskAnswer is { } ma && IsOk(ma) && DecodeSensorMask(ma) is { Count: > 0 } listed
            ? listed.ToHashSet()
            : readings.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToHashSet(); // older firmware: best effort
        if (sensors.Contains(SensorId.GpuFanSpeed))
            sensors.Add(SensorId.GpuTemperature); // reads 0 while the dGPU sleeps, so the fallback above can miss it
        if (hints.CpuFan == true) sensors.Add(SensorId.CpuFanSpeed);
        if (hints.GpuFan == true) sensors.Add(SensorId.GpuFanSpeed);
        if (hints.SystemFan == true) sensors.Add(SensorId.SystemFanSpeed);

        // A fan is there when its RPM sensor is (as Acer's software counts them); system fans only report their speed.
        var fans = FanChannel.Known.Where(f => sensors.Contains(f.RpmSensor)).ToList();

        // CoolBoost (APGeAction). NitroSense offers it only on models without operating modes.
        var actionClass = device.Transport.IsClassAvailable(ActionClass);
        var coolRaw = actionClass ? device.Raw(ActionClass, "GetFunction", CoolBoostGetInput) : null;
        Log($"APGeAction CoolBoost raw={Hex(coolRaw)}");
        var firmwareCoolBoost = coolRaw is { } c && IsOk(c);

        // Dust Defender (the same function, sub-function 1): every sub-function answers, byte 3 tells.
        var dustRaw = actionClass ? device.Raw(ActionClass, "GetFunction", DustDefenderQuery) : null;
        var dustStatusRaw = actionClass ? device.Raw(ActionClass, "GetFunction", DustDefenderStatusQuery) : null;
        Log($"APGeAction Dust Defender raw={Hex(dustRaw)} status raw={Hex(dustStatusRaw)}");
        var dustDefender = dustRaw is { } dust && IsOk(dust) && DustDefenderValue(dust);

        // The embedded controller's fan curves (Get/SetGamingFanTable): where the firmware answers the Get, on the models
        // Acer's software sets them on. The AN515-57's firmware answers, but nothing in it reads the value.
        var fanTableRaw = device.GetFanTableRaw();
        Log($"GetGamingFanTable = {Hex(fanTableRaw)}");
        var fanTable = fanTableRaw is { } ft && IsOk(ft) && UsesFanTable(model);

        // Operating modes. Firmware may list modes on models where Acer's software never uses them. They stay off
        // by default where NitroSense's config says the model has none (MachineType 0/1) or, without that config,
        // where the firmware has CoolBoost instead; the user can still turn them on. (The AN515-57 lists
        // Quiet/Balanced/Performance/Turbo: its firmware hands them to Intel DTT's CPU profiles and NVIDIA's
        // Dynamic Boost, not to the power limit registers.)
        var maskRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.SupportedOperatingModes));
        var modeRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.OperatingMode));
        Log($"misc SupportedOperatingModes raw={Hex(maskRaw)}");
        Log($"misc OperatingMode raw={Hex(modeRaw)}");
        IReadOnlyList<OperatingMode> firmwareModes = [];
        if (maskRaw is { } m && IsOk(m) && DecodeOperatingModeMask(m) is { Count: > 0 } decoded)
            firmwareModes = decoded;
        else if (modeRaw is { } cur && IsOk(cur) && Enum.IsDefined((OperatingMode)MiscValue(cur)))
            firmwareModes = DefaultOperatingModes;
        var modes = hints.MachineType switch
        {
            0 or 1 => [],
            2 when firmwareModes.Count == 0 => DefaultOperatingModes,
            2 => firmwareModes,
            _ when firmwareCoolBoost => [],
            _ => firmwareModes,
        };
        // Where the embedded controller's HID interface sets the mode, its list is the one Acer's software offers.
        if (ecHid is { Modes.Count: > 0 })
            firmwareModes = modes = ecHid.Modes;

        // GPU MUX switch.
        var gpuSupportRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.GpuModeSupport));
        Log($"misc GpuModeSupport raw={Hex(gpuSupportRaw)}");
        Log($"misc GpuMode raw={Hex(device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.GpuMode)))}");
        var gpuSwitch = gpuSupportRaw is { } g && IsOk(g) && MiscValue(g) == 3;

        var coolBoost = firmwareCoolBoost && modes.Count == 0 && hints.MachineType != 2;

        var keyboard = ProbeKeyboard(device, hints, smbios, actionClass, ecHid?.BacklightTimeout == true, Log);
        List<LightingDeviceInfo> lights = [];
        if (keyboard.RgbBacklight)
            lights.Add(EcKeyboardBackend.Describe(keyboard));

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
            (firmwareModes.Count > 0 && modes.Count == 0 ? $" (firmware lists {string.Join(", ", firmwareModes)}; off by default on this model)" : ""));
        Log($"=> CoolBoost: {coolBoost}, GPU mode switch: {gpuSwitch}");
        Log($"=> keyboard: rgb={keyboard.RgbBacklight} zones={keyboard.Zones} ledArray={keyboard.LedArrayLength} " +
            $"autoOff={keyboard.BacklightAutoOff} winKey={keyboard.WindowsKey} overdrive={keyboard.LcdOverdrive}");
        Log($"=> lights: {(lights.Count == 0 ? "none" : string.Join(", ", lights.Select(l => $"{l.Id} ({l.Zones} zones)")))}" +
            $"; light bars: {(lightBars.Count == 0 ? "none" : string.Join(", ", lightBars.Select(b => $"{b.Id} {b.Zones}")))}");
        Log($"=> battery: charge limit={battery.ChargeLimit} calibration={battery.Calibration}; USB charging when off: {usbCharging}; " +
            $"battery-boost flag: {(ecHid?.BatteryBoost == true ? "EC HID" : wmiBatteryBoost ? BatteryBoostValue(boostRaw!.Value) ? "on" : "off" : "absent")}");
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
            Keyboard = keyboard,
            Lights = lights,
            LightBars = lightBars,
            Battery = battery,
            UsbCharging = usbCharging,
            BootAnimation = bootAnimation,
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

    /// <param name="ecHidBacklight">The embedded controller's HID interface keeps the backlight timeout (it then replaces <c>APGeAction</c>'s).</param>
    private static KeyboardCapabilities ProbeKeyboard(AcerDevice device, NitroSenseHints hints, AcerSmbios smbios, bool actionClass,
        bool ecHidBacklight, Action<string> log)
    {
        var backlight = device.GetKeyboardBacklight();
        log($"GetGamingKBBacklight = {(backlight is null ? "error" : Convert.ToHexString(backlight))}");
        var rgb = hints.KeyboardColor switch
        {
            2 => true,
            not null => false,
            null => backlight is not null,
        };

        // Backlight auto-off needs the model's hotkey function number.
        byte? hotkey = hints.BacklightHotkey is > 0 and <= 0xFF ? (byte)hints.BacklightHotkey.Value
            : smbios.HasHotkeyFunction(CommonBacklightHotkey) ? CommonBacklightHotkey
            : null;
        if (ecHidBacklight)
        {
            hotkey = null;
        }
        else if (hotkey is { } h && actionClass)
        {
            var timeout = device.GetBacklightTimeout(h);
            log($"backlight timeout (hotkey 0x{h:X2}) = {(timeout is { } t ? $"{t.Brightness}% / {t.TimeoutSeconds}s" : "error")}");
            if (timeout is null || (hints.Present && !hints.KeyboardSettings.Contains("Backlight", StringComparer.OrdinalIgnoreCase)))
                hotkey = null;
        }
        else
        {
            hotkey = null;
        }

        var profile = device.GetGamingProfile();
        log($"GetGamingProfile(0) = {(profile is { } p ? $"0x{p:X}" : "error")}");
        var windowsKey = hints.Present
            ? hints.KeyboardSettings.Any(s => s.StartsWith("Windowskey", StringComparison.OrdinalIgnoreCase))
            : profile is { } wp && KeyboardProtocol.WindowsKeyValue(wp) is not null;
        // Overdrive depends on the panel, so the firmware's answer counts even where NitroSense lists it for the model.
        var overdrive = profile is { } op && KeyboardProtocol.LcdOverdriveSupported(op)
            && (!hints.Present || hints.AdvancedSettings.Contains("LCD", StringComparer.OrdinalIgnoreCase));

        return new KeyboardCapabilities
        {
            RgbBacklight = rgb,
            Zones = rgb ? Math.Clamp(hints.KeyboardZones ?? 4, 1, 4) : 0,
            Effects = rgb ? KeyboardProtocol.ZonedEffects : [],
            LedArrayLength = smbios.LedArrayLength,
            BacklightHotkey = hotkey,
            EcHidBacklightTimeout = ecHidBacklight,
            WindowsKey = windowsKey && profile is not null,
            LcdOverdrive = overdrive,
            ColorAdjust = hints.ColorAdjust ?? [1, 1, 1],
            DefaultZoneColors = hints.DefaultZoneColors,
        };
    }
}
