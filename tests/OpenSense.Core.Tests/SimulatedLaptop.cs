using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Monitoring;
using static OpenSense.Core.Hardware.AcerProtocol;

namespace OpenSense.Core.Tests;

public enum SimulatedModel
{
    /// <summary>2021-style Nitro: CPU + GPU fans, CoolBoost, no operating modes.</summary>
    Nitro2021,

    /// <summary>2022+ Nitro: operating modes and GPU MUX switch, no CoolBoost.</summary>
    Nitro2022,

    /// <summary>
    /// 2024 Predator: like the 2022 Nitro plus Turbo, a second GPU fan, a system fan that only reports its speed,
    /// Dust Defender, a seven-zone rear light bar (the Infinity Mirror) and a lid logo. No NitroSense hints (PredatorSense
    /// leaves none).
    /// </summary>
    Predator2024,
}

/// <summary>A fake laptop that answers the Acer WMI protocol with a simple thermal model.</summary>
public sealed class SimulatedTransport : IWmiTransport
{
    private readonly object _gate = new();
    private readonly SimulatedModel _model;
    private readonly Random _random;
    private readonly Func<DateTime> _clock;
    private DateTime _last;

    private double _cpuTemp = 46, _gpuTemp = 41, _cpuLoad = 8, _gpuLoad = 2, _loadTarget = 10;
    private double _cpuFanPct, _gpuFanPct, _gpu2FanPct, _systemFanPct;
    private FanBehavior _cpuBehavior = FanBehavior.Auto, _gpuBehavior = FanBehavior.Auto, _gpu2Behavior = FanBehavior.Auto;
    private int _cpuCustom = 50, _gpuCustom = 50, _gpu2Custom = 50;
    private DateTime _dustDefenderUntil = DateTime.MinValue;
    private bool _coolBoost;
    private OperatingMode _opMode = OperatingMode.Balanced;
    private GpuMode _gpuMode = GpuMode.Hybrid;
    private byte _bootAnimation = 1; // answered on every model, as on the AN515-57
    private byte _customBootLogo = 1; // the BIOS setup's "Customize POST animation", on, as on the AN515-57
    private byte _fanTable; // the AN515-57's answer until something picks a curve

    // Keyboard state.
    private byte[] _backlight = KeyboardProtocol.BacklightPayload(KeyboardEffect.Static, 0, 100, KeyboardDirection.Right, RgbColor.White);
    private readonly RgbColor[] _zoneColors = [new(255, 0, 0), new(255, 0, 0), new(255, 0, 0), new(255, 0, 0)];
    private ulong _zonesOn = 0xF;
    private bool _windowsKey = true, _lcdOverdrive;
    private int _backlightTimeout = KeyboardProtocol.AutoOffSeconds, _backlightBrightness = 100;

    // The Predator's lights: a rear bar of seven zones (the Infinity Mirror) and the lid logo.
    private const int MirrorZones = 7;
    private readonly RgbColor[] _mirrorColors = new RgbColor[MirrorZones];
    private int _mirrorZonesOn = (1 << MirrorZones) - 1;
    private byte[] _barBacklight = new byte[16];
    private ulong _logoColor, _logoBehavior;

    // Battery and USB power.
    private bool _healthMode, _calibrating;
    private ulong _usbCharging = 0x6400; // the AN515-57's factory answer: never set

    public SimulatedTransport(SimulatedModel model = SimulatedModel.Nitro2021, int seed = 1, Func<DateTime>? clock = null)
    {
        _model = model;
        _random = new Random(seed);
        _clock = clock ?? (() => DateTime.UtcNow);
        _last = _clock();
    }

    /// <summary>2022 and later: operating modes and a GPU switch instead of CoolBoost.</summary>
    private bool Is2022 => _model is SimulatedModel.Nitro2022 or SimulatedModel.Predator2024;

    private bool IsPredator => _model == SimulatedModel.Predator2024;

    /// <summary>How long a Dust Defender run takes here.</summary>
    public static TimeSpan DustDefenderRun { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Current simulated CPU / GPU utilisation in percent.</summary>
    public (double? Cpu, double? Gpu) Loads
    {
        get
        {
            lock (_gate)
            {
                Step();
                return (Math.Clamp(_cpuLoad, 0, 100), Math.Clamp(_gpuLoad, 0, 100));
            }
        }
    }

    public bool IsClassAvailable(string className) => className is GamingClass or ActionClass or BatteryClass;

    public bool HasArrayInput(string className, string method) => method == "SetGamingKBBacklight";

    public WmiArrayResult InvokeArray(string className, string method, byte[]? input)
    {
        lock (_gate)
        {
            switch (method)
            {
                // Byte 9 is the device: 2 the light bars (PredatorSense's layout), else the keyboard.
                case "SetGamingKBBacklight" when input is { Length: 16 } && input[9] == LightBarProtocol.BacklightDevice:
                    if (!IsPredator)
                        return new WmiArrayResult(Error, null);
                    _barBacklight = [.. input];
                    return new WmiArrayResult(0, null);
                case "SetGamingKBBacklight" when input is { Length: 16 }:
                    _backlight = [.. input];
                    return new WmiArrayResult(0, null);
                case "GetGamingKBBacklight":
                    return new WmiArrayResult(0, [.. _backlight]);
                case "SetGamingLED" when input is { Length: 16 } && input[0] == LightBarProtocol.OnOffGroup && IsPredator:
                    return new WmiArrayResult(SetMirrorZones(input), null);
                case "SetGamingLED" when input is { Length: >= 8 }:
                    return new WmiArrayResult(SetZones(BitConverter.ToUInt64(input)), null);
                default:
                    return new WmiArrayResult(Error, null);
            }
        }
    }

    /// <summary>Like the AN515-57: the 80 % limit and calibration.</summary>
    public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs)
    {
        lock (_gate)
        {
            var args = inputs.ToDictionary(a => a.Name, a => a.Value);
            switch ((className, method))
            {
                case (BatteryClass, "GetBatteryHealthControlStatus"):
                    return FakeFirmware.Outputs(("uFunctionList", 3UL),
                        ("uFunctionStatus", new byte[] { _healthMode ? (byte)1 : (byte)0, _calibrating ? (byte)1 : (byte)0, 0, 0, 0 }),
                        ("uReturn", new byte[2]));
                case (BatteryClass, "SetBatteryHealthControl"):
                    var on = (byte)args["uFunctionStatus"] == 1;
                    switch ((byte)args["uFunctionMask"])
                    {
                        case 1: _healthMode = on; break;
                        case 2: _calibrating = on; break;
                        default: return FakeFirmware.Outputs(("uReturn", 1UL), ("uReservedOut", 0UL));
                    }
                    return FakeFirmware.Outputs(("uReturn", 0UL), ("uReservedOut", 0UL));
                // Firmware with light bars declares the answer as a byte array; the AN515-57's is an integer (status 2).
                case (GamingClass, "GetGamingLED") when Convert.ToUInt32(args["gmInput"], System.Globalization.CultureInfo.InvariantCulture) == LightBarProtocol.LayoutQuery:
                    return IsPredator
                        ? FakeFirmware.Outputs(("gmReturn", 0UL), ("gmOutput", MirrorLayout()))
                        : FakeFirmware.Outputs(("gmOutput", 2UL));
                // The keyboard's zone switches, as the AN515-57 answers them (all four on: 0xF0000000000).
                case (GamingClass, "GetGamingLED") when Convert.ToUInt32(args["gmInput"], System.Globalization.CultureInfo.InvariantCulture) == KeyboardProtocol.ZoneEnableQuery:
                    return FakeFirmware.Outputs(("gmOutput", _zonesOn << 40));
                case (GamingClass, "GetGamingFanTable"):
                    return FakeFirmware.Outputs(("gmOutput", Ok(_fanTable)));
                default:
                    throw new AcerWmiException($"{className}.{method} is not simulated.");
            }
        }
    }

    public bool HealthMode
    {
        get
        {
            lock (_gate)
                return _healthMode;
        }
    }

    /// <summary>Power-off USB charging as the firmware answers it.</summary>
    public ulong UsbCharging
    {
        get
        {
            lock (_gate)
                return _usbCharging;
        }
    }

    private ulong SetUsbCharging(ulong input)
    {
        var state = (input >> 8) & 0xFF;
        var floor = (input >> 16) & 0xFF;
        if (state is not (0x0F or 0x1F) || floor is not (10 or 20 or 30))
            return 0xE4;
        _usbCharging = input & ~0xFFUL;
        return 0;
    }

    /// <summary>Colour zone <paramref name="zone"/> (1..4) currently shows, or null when the zone is off.</summary>
    public RgbColor? ZoneColor(int zone)
    {
        lock (_gate)
            return (_zonesOn & (1UL << (zone - 1))) != 0 ? _zoneColors[zone - 1] : null;
    }

    /// <summary>The current 16-byte keyboard backlight record.</summary>
    public byte[] Backlight
    {
        get
        {
            lock (_gate)
                return [.. _backlight];
        }
    }

    /// <summary>Someone else (Acer's agent) sets the keyboard backlight.</summary>
    public void OverwriteBacklight(byte[] payload)
    {
        lock (_gate)
            _backlight = [.. payload];
    }

    public ulong Invoke(string className, string method, ulong input)
    {
        lock (_gate)
        {
            Step();
            return (className, method) switch
            {
                (GamingClass, "GetGamingSysInfo") => SysInfo((uint)input),
                (GamingClass, "SetGamingFanBehavior") => SetBehavior(input),
                (GamingClass, "SetGamingFanSpeed") => SetSpeed(input),
                (GamingClass, "GetGamingFanBehavior") => GetBehavior(input),
                // Like the real firmware: the last boost written, not what the fan is doing.
                (GamingClass, "GetGamingFanSpeed") => (input & 0xFF) switch
                {
                    1 => Ok((ulong)_cpuCustom),
                    4 => Ok((ulong)_gpuCustom),
                    5 when IsPredator => Ok((ulong)_gpu2Custom),
                    _ => Error,
                },
                (GamingClass, "GetGamingMiscSetting") => GetMisc((MiscSetting)(input & 0xFF)),
                (GamingClass, "SetGamingMiscSetting") => SetMisc((MiscSetting)(input & 0xFF), (byte)(input >> 8)),
                (GamingClass, "SetGamingRgbKb") when ((input >> 32) & 0xFF) != 0 => SetMirrorColor(input),
                (GamingClass, "SetGamingRgbKb") => SetZoneColor(input),
                (GamingClass, "GetGamingRgbKb") => GetZoneColor(input),
                (GamingClass, "SetGamingFanTable") => SetFanTable(input),
                (GamingClass, "GetGamingLEDBehavior") when input == LogoProtocol.Group => IsPredator ? 0UL : 1UL,
                (GamingClass, "SetGamingLEDColor") when IsPredator && (input & 0xFF) == LogoProtocol.Group => SetLogoColor(input),
                (GamingClass, "SetGamingLEDBehavior") when IsPredator && (input & 0xFF) == LogoProtocol.Group => SetLogoBehavior(input),
                (GamingClass, "SetGamingLED") => SetZones(input),
                (GamingClass, "GetGamingProfile") when input == KeyboardProtocol.ProfileQuery =>
                    ((_windowsKey ? 1UL : 0UL) << 24) | (1UL << 32) | ((_lcdOverdrive ? 1UL : 0UL) << 48),
                (GamingClass, "SetGamingProfile") => SetProfile(input),
                (ActionClass, "GetFunction") when (input & 0xFF) == 0x01 && (input & 0x80000) != 0 =>
                    ((ulong)_backlightBrightness << 32) | ((ulong)_backlightTimeout << 40),
                (ActionClass, "SetFunction") when (input & 0xFF) == 0x02 && (input & 0x80000) != 0 => SetBacklightTimeout(input),
                (ActionClass, "GetFunction") when input == UsbChargingQuery => _usbCharging,
                (ActionClass, "SetFunction") when (input & 0xFF) == 0x04 => SetUsbCharging(input),
                // Every sub-function of function 7 answers, as on the AN515-57; byte 3 says what there is.
                (ActionClass, "GetFunction") when input == DustDefenderQuery => (IsPredator ? 1UL << 24 : 0) | Ok(0x100),
                (ActionClass, "GetFunction") when input == DustDefenderStatusQuery => (DustDefenderGoing ? 1UL << 24 : 0) | Ok(0x100),
                (ActionClass, "SetFunction") when IsPredator && input == DustDefenderStartInput => StartDustDefender(),
                (ActionClass, "GetFunction") when !Is2022 && input == CoolBoostGetInput => Ok(_coolBoost ? 1UL : 0UL),
                (ActionClass, "SetFunction") when !Is2022 && (input & 0xFFFF) == 0x07 => SetCoolBoost(input),
                _ => Error,
            };
        }
    }

    private const ulong Error = 0x01;

    private static ulong Ok(ulong value) => value << 8;

    // Like the real AN515-57: bitmap at bit 24 lists CPU temp, CPU fan, system temp, GPU fan, GPU temp.
    // The Predator adds the system fan (sensor 4) and the second GPU fan (sensor 9).
    private ulong SensorMask => (IsPredator ? 0x227UL | 0x8 | 0x100 : 0x227UL) << 24;

    private bool DustDefenderGoing => _clock() < _dustDefenderUntil;

    private ulong StartDustDefender()
    {
        if (!DustDefenderGoing)
            _dustDefenderUntil = _clock() + DustDefenderRun;
        return 0;
    }

    private ulong SysInfo(uint input)
    {
        if (input == SupportedSensorsQuery)
            return SensorMask;
        if (input == BatteryStatusQuery)
            return 1UL << 40; // battery boost available
        if ((input & 0xFF) != 0x01)
            return Error;
        return (SensorId)((input >> 8) & 0xFF) switch
        {
            SensorId.CpuTemperature => Ok((ulong)Math.Round(_cpuTemp)),
            SensorId.GpuTemperature => Ok(_gpuLoad < 3 && _gpuMode == GpuMode.Hybrid ? 0UL : (ulong)Math.Round(_gpuTemp)),
            SensorId.CpuFanSpeed => Ok(Rpm(_cpuFanPct)),
            SensorId.GpuFanSpeed => Ok(Rpm(_gpuFanPct)),
            SensorId.Gpu2FanSpeed when IsPredator => Ok(Rpm(_gpu2FanPct)),
            SensorId.SystemFanSpeed when IsPredator => Ok(Rpm(_systemFanPct)),
            SensorId.SystemTemperature => Ok((ulong)Math.Round((_cpuTemp + _gpuTemp) / 2 - 6)),
            _ => Ok(0), // real firmware answers "ok, 0" for sensors it does not have
        };
    }

    private ulong GetBehavior(ulong input)
    {
        ulong value = 0;
        if ((input & 0x1) != 0) value |= (ulong)_cpuBehavior;
        if ((input & 0x8) != 0) value |= (ulong)_gpuBehavior << 6;
        if (IsPredator && (input & 0x10) != 0) value |= (ulong)_gpu2Behavior << 8;
        return Ok(value);
    }

    private ulong Rpm(double pct) => pct < 1 ? 0 : (ulong)(pct / 100 * 5800 + _random.Next(-40, 40));

    private ulong SetBehavior(ulong input)
    {
        if ((input & 0x1) != 0) _cpuBehavior = (FanBehavior)((input >> 16) & 0x3);
        if ((input & 0x8) != 0) _gpuBehavior = (FanBehavior)((input >> 22) & 0x3);
        if ((input & 0x10) != 0)
        {
            if (!IsPredator)
                return Error;
            _gpu2Behavior = (FanBehavior)((input >> 24) & 0x3);
        }
        return 0;
    }

    private ulong SetSpeed(ulong input)
    {
        var pct = (int)Math.Min(100, (input >> 8) & 0xFF);
        switch (input & 0xFF)
        {
            case 1: _cpuCustom = pct; return 0;
            case 4: _gpuCustom = pct; return 0;
            case 5 when IsPredator: _gpu2Custom = pct; return 0;
            default: return Error;
        }
    }

    private ulong GetMisc(MiscSetting setting) => setting switch
    {
        MiscSetting.OperatingMode when Is2022 => Ok((ulong)_opMode),
        MiscSetting.SupportedOperatingModes when Is2022 =>
            Ok((1UL << (int)OperatingMode.Quiet) | (1UL << (int)OperatingMode.Balanced) | (1UL << (int)OperatingMode.Performance)
                | (IsPredator ? 1UL << (int)OperatingMode.Turbo : 0)),
        MiscSetting.GpuModeSupport => Ok(Is2022 ? 3UL : 0UL),
        MiscSetting.GpuMode when Is2022 => Ok((ulong)_gpuMode),
        MiscSetting.BootAnimation => Ok(_bootAnimation),
        MiscSetting.CustomBootLogo => Ok(CustomBootLogoSwitch ? _customBootLogo : 0xFFUL),
        _ => Error,
    };

    /// <summary>
    /// The BIOS has the setup's switch for the custom boot logo, as the AN515-57 V1.17 has; without it, misc 8 answers 0xFF
    /// like any id the firmware lacks.
    /// </summary>
    public bool CustomBootLogoSwitch { get; init; } = true;

    private ulong SetMisc(MiscSetting setting, byte value)
    {
        switch (setting)
        {
            case MiscSetting.OperatingMode when Is2022 && Enum.IsDefined((OperatingMode)value):
                _opMode = (OperatingMode)value;
                return 0;
            case MiscSetting.GpuMode when Is2022 && Enum.IsDefined((GpuMode)value):
                _gpuMode = (GpuMode)value;
                return 0;
            case MiscSetting.BootAnimation when value is 0 or 1:
                _bootAnimation = value;
                return 0;
            case MiscSetting.CustomBootLogo when CustomBootLogoSwitch && value is 0 or 1:
                _customBootLogo = value;
                return 0;
            default:
                return Error;
        }
    }

    /// <summary>The BIOS setup's switch for the custom boot logo; the test can turn it off as a user would in the setup.</summary>
    public bool CustomBootLogoShown
    {
        get
        {
            lock (_gate)
                return _customBootLogo == 1;
        }
        set
        {
            lock (_gate)
                _customBootLogo = value ? (byte)1 : (byte)0;
        }
    }

    /// <summary>The fan curve the embedded controller runs (0 until something picks one).</summary>
    public byte FanTable
    {
        get
        {
            lock (_gate)
                return _fanTable;
        }
    }

    /// <summary>Like the AN515-57 V1.17, which keeps byte 0 whatever it is.</summary>
    private ulong SetFanTable(ulong input)
    {
        _fanTable = (byte)input;
        return 0;
    }

    private ulong GetZoneColor(ulong input)
    {
        var zone = System.Numerics.BitOperations.TrailingZeroCount(input & 0xF);
        if ((input & 0xF) == 0 || (input & ~0xFUL) != 0)
            return 2;
        var color = _zoneColors[zone];
        return Ok(color.R | ((ulong)color.G << 8) | ((ulong)color.B << 16));
    }

    private ulong SetZoneColor(ulong input)
    {
        var mask = input & 0xF;
        for (var zone = 0; zone < 4; zone++)
        {
            if ((mask & (1UL << zone)) != 0)
                _zoneColors[zone] = new RgbColor((byte)(input >> 8), (byte)(input >> 16), (byte)(input >> 24));
        }
        return mask == 0 ? Error : 0;
    }

    private ulong SetZones(ulong input)
    {
        if ((input & 0xFF) != 0x08)
            return Error;
        _zonesOn = (input >> 40) & 0xF;
        return 0;
    }

    /// <summary>The 16-byte layout's answer (without the status): only the rear bar, two zone fields per zone.</summary>
    private byte[] MirrorLayout()
    {
        var output = new byte[15];
        output.AsSpan(5, 6).Fill(0xFF); // no front, left or right bar
        var fields = 0;
        for (var zone = 0; zone < 8; zone++)
            fields |= (zone >= MirrorZones ? 0b11 : (_mirrorZonesOn >> zone) & 1) << (zone * 2);
        output[11] = (byte)fields;
        output[12] = (byte)(fields >> 8);
        return output;
    }

    private ulong SetMirrorZones(byte[] input)
    {
        // Only the rear bar exists; the others' bytes must be 0xFF (left alone).
        if (input.AsSpan(6, 6).ContainsAnyExcept((byte)0xFF))
            return Error;
        var fields = input[12] | (input[13] << 8);
        _mirrorZonesOn = 0;
        for (var zone = 0; zone < MirrorZones; zone++)
            _mirrorZonesOn |= ((fields >> (zone * 2)) & 1) << zone;
        return 0;
    }

    private ulong SetMirrorColor(ulong input)
    {
        var bar = (input >> 32) & 0xFF;
        var zoneBits = (input >> 40) & 0xFF;
        if (!IsPredator || bar != (ulong)LightBarId.Rear || zoneBits == 0 || (input & 0xFF) != 0)
            return Error;
        var zone = System.Numerics.BitOperations.TrailingZeroCount(zoneBits);
        if (zone >= MirrorZones)
            return Error;
        _mirrorColors[zone] = new RgbColor((byte)(input >> 8), (byte)(input >> 16), (byte)(input >> 24));
        return 0;
    }

    private ulong SetLogoColor(ulong input)
    {
        _logoColor = input;
        return 0;
    }

    private ulong SetLogoBehavior(ulong input)
    {
        _logoBehavior = input;
        return 0;
    }

    /// <summary>What Infinity Mirror zone <paramref name="zone"/> (1..7) shows, or null when it is off.</summary>
    public RgbColor? MirrorColor(int zone)
    {
        lock (_gate)
            return ((_mirrorZonesOn >> (zone - 1)) & 1) != 0 ? _mirrorColors[zone - 1] : null;
    }

    /// <summary>The last effect record sent to the light bars.</summary>
    public byte[] LightBarBacklight
    {
        get
        {
            lock (_gate)
                return [.. _barBacklight];
        }
    }

    /// <summary>The logo's last <c>SetGamingLEDColor</c> and <c>SetGamingLEDBehavior</c> inputs.</summary>
    public (ulong Color, ulong Behavior) Logo
    {
        get
        {
            lock (_gate)
                return (_logoColor, _logoBehavior);
        }
    }

    private ulong SetProfile(ulong input)
    {
        switch (input & 0xFF)
        {
            case 0x02: _windowsKey = ((input >> 24) & 1) == 1; return 0;
            case 0x10: _lcdOverdrive = ((input >> 48) & 1) == 1; return 0;
            default: return Error;
        }
    }

    private ulong SetBacklightTimeout(ulong input)
    {
        _backlightBrightness = (int)((input >> 32) & 0xFF);
        _backlightTimeout = (int)((input >> 40) & 0xFF);
        return 0;
    }

    private ulong SetCoolBoost(ulong input)
    {
        _coolBoost = ((input >> 16) & 1) == 1;
        return 0;
    }

    /// <summary>Advances the thermal model to "now".</summary>
    private void Step()
    {
        var now = _clock();
        var dt = Math.Clamp((now - _last).TotalSeconds, 0, 5);
        _last = now;
        if (dt <= 0)
            return;

        // Load: drifts toward a target that occasionally jumps (idle / browsing / gaming bursts).
        if (_random.NextDouble() < dt / 12)
            _loadTarget = _random.NextDouble() switch { < 0.45 => 6, < 0.75 => 28, _ => 85 };
        _cpuLoad = Approach(_cpuLoad, _loadTarget + _random.NextDouble() * 8 - 4, dt, 1.5);
        _gpuLoad = Approach(_gpuLoad, _loadTarget > 60 ? 92 : _loadTarget > 20 ? 12 : 0, dt, 2);

        var modeHeat = _opMode switch { OperatingMode.Quiet => 0.8, OperatingMode.Performance => 1.15, OperatingMode.Turbo => 1.25, _ => 1.0 };
        _cpuFanPct = Approach(_cpuFanPct, FanTarget(_cpuBehavior, _cpuCustom, _cpuTemp), dt, 1.2);
        _gpuFanPct = Approach(_gpuFanPct, FanTarget(_gpuBehavior, _gpuCustom, _gpuTemp), dt, 1.2);
        _gpu2FanPct = Approach(_gpu2FanPct, FanTarget(_gpu2Behavior, _gpu2Custom, _gpuTemp), dt, 1.2);
        _systemFanPct = Approach(_systemFanPct, FanTarget(FanBehavior.Auto, 0, (_cpuTemp + _gpuTemp) / 2), dt, 1.2);

        var cpuEq = 36 + _cpuLoad * 0.62 * modeHeat - (_cpuFanPct - 35) * 0.2;
        var gpuEq = 34 + _gpuLoad * 0.5 * modeHeat - (_gpuFanPct - 35) * 0.16;
        _cpuTemp = Math.Clamp(Approach(_cpuTemp, cpuEq, dt, 6), 30, 99);
        _gpuTemp = Math.Clamp(Approach(_gpuTemp, gpuEq, dt, 9), 30, 92);
    }

    /// <summary>
    /// Custom boosts Auto: it moves the fan <paramref name="custom"/> % of the way to full speed, counted like the
    /// real embedded controller in whole tens of the percentage and of the room left above Auto, both rounded down.
    /// </summary>
    private double FanTarget(FanBehavior behavior, int custom, double temp)
    {
        var auto = Math.Clamp((temp - 42) * 1.9 + 22 + (_coolBoost ? 15 : 0), temp < 45 ? 0 : 22, 100);
        return behavior switch
        {
            FanBehavior.Max => 100,
            FanBehavior.Custom when custom >= 100 => 100,
            FanBehavior.Custom => auto + custom / 10 * Math.Floor((100 - auto) / 10),
            _ => auto,
        };
    }

    private static double Approach(double value, double target, double dt, double tau) =>
        value + (target - value) * (1 - Math.Exp(-dt / tau));

    public void Dispose() { }
}

/// <summary>The engine's view of a <see cref="SimulatedTransport"/> laptop, with the hints NitroSense would leave behind.</summary>
public sealed class SimulatedMachine(SimulatedModel model) : IMachine
{
    /// <summary>The opened laptop's load; the engine owns (and disposes) the laptop itself.</summary>
    private Func<(double? Cpu, double? Gpu)>? _loads;

    public string? Model => ModelName ?? model switch
    {
        SimulatedModel.Nitro2022 => "Nitro AN515-58 (simulated)",
        SimulatedModel.Predator2024 => "Predator PHN16-72 (simulated)",
        _ => "Nitro AN515-57 (simulated)",
    };

    /// <summary>Another model name than the simulated model's.</summary>
    internal string? ModelName { get; init; }

    public string? BiosVersion => "simulated";

    /// <summary>Made up, in the format of an Acer label.</summary>
    public string? SerialNumber => "NHQ7PEU00A1230ABCD7600";

    internal FakeFirmwareEvents Events { get; } = new();

    internal FakePower Power { get; } = new();

    public IWmiTransport OpenFirmware()
    {
        var laptop = new SimulatedTransport(model) { CustomBootLogoSwitch = CustomBootLogoSwitch, CustomBootLogoShown = CustomBootLogoShown };
        _loads = () => laptop.Loads;
        Laptop = laptop;
        return laptop;
    }

    /// <summary>The firmware the engine opened last.</summary>
    internal SimulatedTransport? Laptop { get; private set; }

    /// <summary>The BIOS has the setup's switch for the custom boot logo (see <see cref="SimulatedTransport.CustomBootLogoSwitch"/>).</summary>
    internal bool CustomBootLogoSwitch { get; init; } = true;

    /// <summary>The setup's switch at start: on, as on the AN515-57.</summary>
    internal bool CustomBootLogoShown { get; init; } = true;

    public IFirmwareEvents OpenFirmwareEvents() => Events;

    public IPowerSource OpenPowerSource() => Power;

    public ISystemPower OpenSystemPower() => SystemPower;

    internal FakeSystemPower SystemPower { get; } = new();

    /// <summary>The AN515-57's battery as Windows reported it (2026-09-25).</summary>
    internal BatteryHealth? Battery { get; init; } = new(58751, 37853, 74, false) { Manufacturer = "SMP", Name = "AP18E7M" };

    public BatteryHealth? ReadBatteryHealth() => Battery;

    public IHidBus OpenHid() => Hid;

    internal FakeHidBus Hid { get; } = new();

    public NitroSenseHints ReadHints() => model == SimulatedModel.Predator2024 ? new NitroSenseHints() : new()
    {
        MachineType = model == SimulatedModel.Nitro2022 ? 2 : 1,
        CpuFan = true,
        GpuFan = true,
        KeyboardColor = 2,
        KeyboardZones = 4,
        BacklightHotkey = 0x84,
        KeyboardSettings = ["Backlight", "Sticky_Key", "Windowskey1"],
        AdvancedSettings = ["LCD", "Temperature"],
    };

    public AcerSmbios ReadSmbios() => Smbios;

    /// <summary>Acer's SMBIOS tables: the Predator's (interface 2.91, RGB keyboard, EC light bars), else none unless a test gives some.</summary>
    internal AcerSmbios Smbios { get; init; } = model == SimulatedModel.Predator2024 ? PredatorSmbios : AcerSmbios.Empty;

    internal static AcerSmbios PredatorSmbios { get; } = new(2, 0x5B,
        [new(0x08, 4), new(0x0A, 2), new((byte)GamingRecord.LightBar, 1)], []);

    public ILoadMonitor OpenLoadMonitor() => new SimulatedLoadMonitor(_loads ?? throw new InvalidOperationException("Open the firmware first."));

    public DirectSensors OpenSensors() => GpuClocks is { } clocks
        ? new DirectSensors(null, new FakeGpuSensor { Limit = GpuLimit }, new FakeGpuPowerState { On = true }, SensorStatus.NotUsed,
            new SensorStatus(ChipSensor.NvidiaDriver), clocks)
        : DirectSensors.None;

    /// <summary>An NVIDIA driver with clock offsets, and its GPU on; none unless a test gives one.</summary>
    internal FakeGpuClocks? GpuClocks { get; init; }

    /// <summary>The temperature that driver says the GPU is held under; none unless a test gives one.</summary>
    internal int? GpuLimit { get; init; }

    public IBootLogoStore? OpenBootLogo(Action<string> log) => SystemPartition is { } root ? new BootLogoFolder(root, () => FreeSpace) : null;

    /// <summary>A folder standing in for the EFI system partition; none unless a test gives one.</summary>
    internal string? SystemPartition { get; init; }

    internal long? FreeSpace { get; set; }

    public PixelSize? ReadInternalScreen() => Screen;

    internal PixelSize? Screen { get; set; } = new(1920, 1080);
}

/// <summary>Reads the simulated laptop's load.</summary>
public sealed class SimulatedLoadMonitor(Func<(double? Cpu, double? Gpu)> loads) : ILoadMonitor
{
    public string? CpuName => "Intel Core i7-11800H (simulated)";

    public string? GpuName => "NVIDIA GeForce RTX 3060 Laptop GPU (simulated)";

    public (double? Cpu, double? Gpu) Sample() => loads();

    public void Dispose()
    {
    }
}
