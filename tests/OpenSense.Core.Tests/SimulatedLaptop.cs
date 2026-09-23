using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;
using static OpenSense.Core.Hardware.AcerProtocol;

namespace OpenSense.Core.Tests;

public enum SimulatedModel
{
    /// <summary>2021-style Nitro: CPU + GPU fans, CoolBoost, no operating modes.</summary>
    Nitro2021,

    /// <summary>2022+ Nitro: operating modes and GPU MUX switch, no CoolBoost.</summary>
    Nitro2022,
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
    private double _cpuFanPct, _gpuFanPct;
    private FanBehavior _cpuBehavior = FanBehavior.Auto, _gpuBehavior = FanBehavior.Auto;
    private int _cpuCustom = 50, _gpuCustom = 50;
    private bool _coolBoost;
    private OperatingMode _opMode = OperatingMode.Balanced;
    private GpuMode _gpuMode = GpuMode.Hybrid;

    // Keyboard state.
    private byte[] _backlight = KeyboardProtocol.BacklightPayload(KeyboardEffect.Static, 0, 100, KeyboardDirection.Right, RgbColor.White);
    private readonly RgbColor[] _zoneColors = [new(255, 0, 0), new(255, 0, 0), new(255, 0, 0), new(255, 0, 0)];
    private ulong _zonesOn = 0xF;
    private bool _windowsKey = true, _lcdOverdrive;
    private int _backlightTimeout = KeyboardProtocol.AutoOffSeconds, _backlightBrightness = 100;

    public SimulatedTransport(SimulatedModel model = SimulatedModel.Nitro2021, int seed = 1, Func<DateTime>? clock = null)
    {
        _model = model;
        _random = new Random(seed);
        _clock = clock ?? (() => DateTime.UtcNow);
        _last = _clock();
    }

    private bool Is2022 => _model == SimulatedModel.Nitro2022;

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

    public bool IsClassAvailable(string className) => className is GamingClass or ActionClass;

    public bool HasArrayInput(string className, string method) => method == "SetGamingKBBacklight";

    public WmiArrayResult InvokeArray(string className, string method, byte[]? input)
    {
        lock (_gate)
        {
            switch (method)
            {
                case "SetGamingKBBacklight" when input is { Length: 16 }:
                    _backlight = [.. input];
                    return new WmiArrayResult(0, null);
                case "GetGamingKBBacklight":
                    return new WmiArrayResult(0, [.. _backlight]);
                case "SetGamingLED" when input is { Length: >= 8 }:
                    return new WmiArrayResult(SetZones(BitConverter.ToUInt64(input)), null);
                default:
                    return new WmiArrayResult(Error, null);
            }
        }
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
                (GamingClass, "GetGamingFanSpeed") => (input & 0xFF) switch
                {
                    1 => Ok((ulong)Math.Round(_cpuFanPct)),
                    4 => Ok((ulong)Math.Round(_gpuFanPct)),
                    _ => Error,
                },
                (GamingClass, "GetGamingMiscSetting") => GetMisc((MiscSetting)(input & 0xFF)),
                (GamingClass, "SetGamingMiscSetting") => SetMisc((MiscSetting)(input & 0xFF), (byte)(input >> 8)),
                (GamingClass, "SetGamingRgbKb") => SetZoneColor(input),
                (GamingClass, "SetGamingLED") => SetZones(input),
                (GamingClass, "GetGamingProfile") when input == KeyboardProtocol.ProfileQuery =>
                    ((_windowsKey ? 1UL : 0UL) << 24) | (1UL << 32) | ((_lcdOverdrive ? 1UL : 0UL) << 48),
                (GamingClass, "SetGamingProfile") => SetProfile(input),
                (ActionClass, "GetFunction") when (input & 0xFF) == 0x01 && (input & 0x80000) != 0 =>
                    ((ulong)_backlightBrightness << 32) | ((ulong)_backlightTimeout << 40),
                (ActionClass, "SetFunction") when (input & 0xFF) == 0x02 && (input & 0x80000) != 0 => SetBacklightTimeout(input),
                (ActionClass, "GetFunction") when !Is2022 && input == CoolBoostGetInput => Ok(_coolBoost ? 1UL : 0UL),
                (ActionClass, "SetFunction") when !Is2022 && (input & 0xFF) == 0x07 => SetCoolBoost(input),
                _ => Error,
            };
        }
    }

    private const ulong Error = 0x01;

    private static ulong Ok(ulong value) => value << 8;

    // Like the real AN515-57: bitmap at bit 24 lists CPU temp, CPU fan, system temp, GPU fan, GPU temp.
    private const ulong SensorMask = 0x227UL << 24;

    private ulong SysInfo(uint input)
    {
        if (input == SupportedSensorsQuery)
            return SensorMask;
        if ((input & 0xFF) != 0x01)
            return Error;
        return (SensorId)((input >> 8) & 0xFF) switch
        {
            SensorId.CpuTemperature => Ok((ulong)Math.Round(_cpuTemp)),
            SensorId.GpuTemperature => Ok(_gpuLoad < 3 && _gpuMode == GpuMode.Hybrid ? 0UL : (ulong)Math.Round(_gpuTemp)),
            SensorId.CpuFanSpeed => Ok(Rpm(_cpuFanPct)),
            SensorId.GpuFanSpeed => Ok(Rpm(_gpuFanPct)),
            SensorId.SystemTemperature => Ok((ulong)Math.Round((_cpuTemp + _gpuTemp) / 2 - 6)),
            _ => Ok(0), // real firmware answers "ok, 0" for sensors it does not have
        };
    }

    private ulong GetBehavior(ulong input)
    {
        ulong value = 0;
        if ((input & 0x1) != 0) value |= (ulong)_cpuBehavior;
        if ((input & 0x8) != 0) value |= (ulong)_gpuBehavior << 6;
        return Ok(value);
    }

    private ulong Rpm(double pct) => pct < 1 ? 0 : (ulong)(pct / 100 * 5800 + _random.Next(-40, 40));

    private ulong SetBehavior(ulong input)
    {
        if ((input & 0x1) != 0) _cpuBehavior = (FanBehavior)((input >> 16) & 0x3);
        if ((input & 0x8) != 0) _gpuBehavior = (FanBehavior)((input >> 22) & 0x3);
        return 0;
    }

    private ulong SetSpeed(ulong input)
    {
        var pct = (int)Math.Min(100, (input >> 8) & 0xFF);
        switch (input & 0xFF)
        {
            case 1: _cpuCustom = pct; return 0;
            case 4: _gpuCustom = pct; return 0;
            default: return Error;
        }
    }

    private ulong GetMisc(MiscSetting setting) => setting switch
    {
        MiscSetting.OperatingMode when Is2022 => Ok((ulong)_opMode),
        MiscSetting.SupportedOperatingModes when Is2022 =>
            Ok((1UL << (int)OperatingMode.Quiet) | (1UL << (int)OperatingMode.Balanced) | (1UL << (int)OperatingMode.Performance)),
        MiscSetting.GpuModeSupport => Ok(Is2022 ? 3UL : 0UL),
        MiscSetting.GpuMode when Is2022 => Ok((ulong)_gpuMode),
        _ => Error,
    };

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
            default:
                return Error;
        }
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

        var cpuEq = 36 + _cpuLoad * 0.62 * modeHeat - (_cpuFanPct - 35) * 0.2;
        var gpuEq = 34 + _gpuLoad * 0.5 * modeHeat - (_gpuFanPct - 35) * 0.16;
        _cpuTemp = Math.Clamp(Approach(_cpuTemp, cpuEq, dt, 6), 30, 99);
        _gpuTemp = Math.Clamp(Approach(_gpuTemp, gpuEq, dt, 9), 30, 92);
    }

    private double FanTarget(FanBehavior behavior, int custom, double temp) => behavior switch
    {
        FanBehavior.Max => 100,
        FanBehavior.Custom => custom,
        _ => Math.Clamp((temp - 42) * 1.9 + 22 + (_coolBoost ? 15 : 0), temp < 45 ? 0 : 22, 100),
    };

    private static double Approach(double value, double target, double dt, double tau) =>
        value + (target - value) * (1 - Math.Exp(-dt / tau));

    public void Dispose() { }
}

/// <summary>The engine's view of a <see cref="SimulatedTransport"/> laptop, with the hints NitroSense would leave behind.</summary>
public sealed class SimulatedMachine(SimulatedModel model) : IMachine
{
    /// <summary>The opened laptop's load; the engine owns (and disposes) the laptop itself.</summary>
    private Func<(double? Cpu, double? Gpu)>? _loads;

    public string? Model => model == SimulatedModel.Nitro2022 ? "Nitro AN515-58 (simulated)" : "Nitro AN515-57 (simulated)";

    public string? BiosVersion => "simulated";

    public IWmiTransport OpenFirmware()
    {
        var laptop = new SimulatedTransport(model);
        _loads = () => laptop.Loads;
        return laptop;
    }

    public NitroSenseHints ReadHints() => new()
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

    public AcerSmbios ReadSmbios() => AcerSmbios.Empty;

    public ILoadMonitor OpenLoadMonitor() => new SimulatedLoadMonitor(_loads ?? throw new InvalidOperationException("Open the firmware first."));

    public DirectSensors OpenSensors() => DirectSensors.None;
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
