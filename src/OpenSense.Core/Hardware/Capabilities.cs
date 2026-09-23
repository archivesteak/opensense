using System.Globalization;
using System.Text;
using Microsoft.Win32;
using static OpenSense.Core.Hardware.AcerProtocol;

namespace OpenSense.Core.Hardware;

public sealed record KeyboardCapabilities
{
    public static KeyboardCapabilities None { get; } = new();

    /// <summary>Zoned RGB backlight with effects.</summary>
    public bool RgbBacklight { get; init; }

    public int Zones { get; init; }

    /// <summary><c>SetGamingLED</c> takes a byte array on this firmware (gaming interface ≥ 2.86).</summary>
    public bool ArrayZoneCommand { get; init; }

    /// <summary>Keyboard-backlight hotkey function number, when backlight auto-off is available.</summary>
    public byte? BacklightHotkey { get; init; }

    public bool WindowsKey { get; init; }
    public bool LcdOverdrive { get; init; }

    /// <summary>Per-channel colour correction NitroSense applies for this model (R, G, B).</summary>
    public IReadOnlyList<double> ColorAdjust { get; init; } = [1, 1, 1];

    public IReadOnlyList<RgbColor> DefaultZoneColors { get; init; } = [];

    public bool BacklightAutoOff => BacklightHotkey is not null;
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

    public AcerSmbios Smbios { get; init; } = AcerSmbios.Empty;

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

    public DeviceCapabilities Apply(DeviceCapabilities caps)
    {
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

    public static DeviceCapabilities Probe(AcerDevice device, NitroSenseHints? hints = null, AcerSmbios? smbios = null)
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

        // A fan is controllable when its RPM sensor exists.
        var fans = FanChannel.Known.Where(f => sensors.Contains(f.RpmSensor)).ToList();

        // CoolBoost (APGeAction). NitroSense offers it only on models without operating modes.
        var actionClass = device.Transport.IsClassAvailable(ActionClass);
        var coolRaw = actionClass ? device.Raw(ActionClass, "GetFunction", CoolBoostGetInput) : null;
        Log($"APGeAction CoolBoost raw={Hex(coolRaw)}");
        var firmwareCoolBoost = coolRaw is { } c && IsOk(c);

        // Operating modes. Firmware may list modes on models where Acer's software never uses them: the
        // AN515-57 lists Quiet/Balanced/Performance/Turbo, accepts them and changes nothing (same power
        // limits, package power and fans in each; measured). They stay off by default where NitroSense's
        // config says the model has none (MachineType 0/1) or, without that config, where the firmware
        // has CoolBoost instead; the user can still turn them on.
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

        // GPU MUX switch.
        var gpuSupportRaw = device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.GpuModeSupport));
        Log($"misc GpuModeSupport raw={Hex(gpuSupportRaw)}");
        Log($"misc GpuMode raw={Hex(device.Raw(GamingClass, "GetGamingMiscSetting", MiscGetInput(MiscSetting.GpuMode)))}");
        var gpuSwitch = gpuSupportRaw is { } g && IsOk(g) && MiscValue(g) == 3;

        var coolBoost = firmwareCoolBoost && modes.Count == 0 && hints.MachineType != 2;

        var keyboard = ProbeKeyboard(device, hints, smbios, actionClass, Log);

        // Extra read-only probes that help map new models.
        foreach (var (method, input) in new (string, uint)[]
                 {
                     ("GetGamingFanBehavior", 0x1), ("GetGamingFanBehavior", 0x8), ("GetGamingFanBehavior", 0x9),
                     ("GetGamingFanSpeed", 0x1), ("GetGamingFanSpeed", 0x4),
                 })
            Log($"{method}(0x{input:X}) = {Hex(device.Raw(GamingClass, method, input))}");

        Log($"=> sensors: {string.Join(", ", sensors)}");
        Log($"=> fans: {string.Join(", ", fans.Select(f => f.Name))}");
        Log($"=> operating modes: {(modes.Count == 0 ? "none" : string.Join(", ", modes))}" +
            (firmwareModes.Count > 0 && modes.Count == 0 ? $" (firmware lists {string.Join(", ", firmwareModes)}; off by default on this model)" : ""));
        Log($"=> CoolBoost: {coolBoost}, GPU mode switch: {gpuSwitch}");
        Log($"=> keyboard: rgb={keyboard.RgbBacklight} zones={keyboard.Zones} arrayZones={keyboard.ArrayZoneCommand} " +
            $"autoOff={keyboard.BacklightAutoOff} winKey={keyboard.WindowsKey} overdrive={keyboard.LcdOverdrive}");

        return new DeviceCapabilities(true, sensors, fans, coolBoost, modes, gpuSwitch, diag.ToString())
        {
            FirmwareOperatingModes = firmwareModes,
            FirmwareCoolBoost = firmwareCoolBoost,
            Keyboard = keyboard,
            Smbios = smbios,
        };
    }

    private static KeyboardCapabilities ProbeKeyboard(AcerDevice device, NitroSenseHints hints, AcerSmbios smbios, bool actionClass, Action<string> log)
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
        if (hotkey is { } h && actionClass)
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
        var overdrive = hints.Present
            ? hints.AdvancedSettings.Contains("LCD", StringComparer.OrdinalIgnoreCase)
            : profile is { } op && ((op >> 48) & 0xFF) is 0 or 1;

        return new KeyboardCapabilities
        {
            RgbBacklight = rgb,
            Zones = rgb ? Math.Clamp(hints.KeyboardZones ?? 4, 1, 4) : 0,
            ArrayZoneCommand = smbios.UsesArrayLedBehavior,
            BacklightHotkey = hotkey,
            WindowsKey = windowsKey && profile is not null,
            LcdOverdrive = overdrive && profile is not null,
            ColorAdjust = hints.ColorAdjust ?? [1, 1, 1],
            DefaultZoneColors = hints.DefaultZoneColors,
        };
    }
}
