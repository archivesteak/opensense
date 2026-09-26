using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Tests;

/// <summary>Scriptable firmware: tests set temperatures directly and inspect every call made.</summary>
internal sealed class FakeFirmware : IWmiTransport
{
    public int? CpuTemp { get; set; } = 60;
    public int GpuTemp { get; set; } = 55;
    public bool FailCpuTemp { get; set; }
    public bool SupportsModes { get; set; }

    /// <summary>The supported operating modes by mode value: Quiet, Balanced, Performance and Turbo by default.</summary>
    public ulong ModeMask { get; set; } = 0x33;

    /// <summary>The battery-boost flag (<c>GetGamingSysInfo(0x02)</c> byte 5): off when the battery is too low to help the adapter.</summary>
    public bool BatteryBoost { get; set; } = true;

    /// <summary>Answers the CoolBoost query (APGeAction), as 2021-and-earlier Nitro firmware does.</summary>
    public bool SupportsCoolBoost { get; set; }
    public bool RejectEverything { get; set; }

    /// <summary>The supported-sensor bitmap (bits 24+), the AN515-57's by default: CPU and GPU fans only.</summary>
    public ulong SensorMask { get; set; } = 0x227UL << 24;

    /// <summary>Behaviour by group bit.</summary>
    public Dictionary<int, FanBehavior> Behavior { get; } = new() { [0] = FanBehavior.Auto, [3] = FanBehavior.Auto, [4] = FanBehavior.Auto };
    public Dictionary<int, int> Speed { get; } = new();

    /// <summary>Has Dust Defender (APGeAction function 7, sub-functions 1 and 3).</summary>
    public bool SupportsDustDefender { get; set; }
    public bool DustDefenderRunning { get; set; }

    /// <summary>Turns a Dust Defender start down with status 0xE5, as a busy embedded controller does.</summary>
    public bool DustDefenderBusy { get; set; }
    public OperatingMode Mode { get; private set; } = OperatingMode.Balanced;
    public bool CoolBoost { get; private set; }
    public List<(string Method, ulong Input)> Calls { get; } = [];

    public IEnumerable<(string Method, ulong Input)> Writes => Calls.Where(c => c.Method.StartsWith("Set", StringComparison.Ordinal));

    public bool IsClassAvailable(string className) => true;

    public List<(string Method, byte[] Input)> ArrayCalls { get; } = [];

    public bool HasArrayInput(string className, string method) => method == "SetGamingKBBacklight";

    public WmiArrayResult InvokeArray(string className, string method, byte[]? input)
    {
        ArrayCalls.Add((method, input ?? []));
        return method.StartsWith("Set", StringComparison.Ordinal) && !RejectEverything
            ? new WmiArrayResult(0, null)
            : new WmiArrayResult(1, null);
    }

    /// <summary>Has <c>BatteryControl</c> with the 80 % limit and calibration, like the AN515-57.</summary>
    public bool SupportsBattery { get; set; }
    public bool HealthMode { get; set; }
    public bool Calibrating { get; set; }

    /// <summary>Has power-off USB charging (APGeAction function 4); starts as the AN515-57's factory answer.</summary>
    public bool SupportsUsbCharging { get; set; }
    public ulong UsbCharging { get; set; } = 0x6400;

    /// <summary>Has <c>Get/SetGamingFanTable</c>; the table starts as the AN515-57's (0, none picked).</summary>
    public bool SupportsFanTable { get; set; }
    public byte FanTable { get; set; }

    /// <summary>The <c>GetGamingProfile(0)</c> answer, or none (an error).</summary>
    public ulong? Profile { get; set; }

    public List<(string Method, Dictionary<string, object> Inputs)> NamedCalls { get; } = [];

    public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs)
    {
        var args = inputs.ToDictionary(a => a.Name, a => a.Value);
        NamedCalls.Add((method, args));
        if (SupportsFanTable && className == AcerProtocol.GamingClass && method == "GetGamingFanTable")
            return Outputs(("gmOutput", (ulong)FanTable << 8));
        if (!SupportsBattery || className != AcerProtocol.BatteryClass)
            throw new AcerWmiException($"{className}.{method} is not scripted.");
        switch (method)
        {
            case "GetBatteryHealthControlStatus":
                return Outputs(("uFunctionList", 3UL), ("uFunctionStatus", new byte[] { Flag(HealthMode), Flag(Calibrating), 0, 0, 0 }),
                    ("uReturn", new byte[2]));
            case "SetBatteryHealthControl" when RejectEverything:
                return Outputs(("uReturn", 1UL), ("uReservedOut", 0UL));
            case "SetBatteryHealthControl":
                var on = (byte)args["uFunctionStatus"] == 1;
                switch ((byte)args["uFunctionMask"])
                {
                    case 1: HealthMode = on; break;
                    case 2: Calibrating = on; break;
                }
                return Outputs(("uReturn", 0UL), ("uReservedOut", 0UL));
            default:
                throw new AcerWmiException($"{className}.{method} is not scripted.");
        }

        static byte Flag(bool on) => on ? (byte)1 : (byte)0;
    }

    internal static WmiOutputs Outputs(params (string Name, object Value)[] values) =>
        new(values.Select(v => KeyValuePair.Create(v.Name, v.Value)));

    public ulong Invoke(string className, string method, ulong input)
    {
        Calls.Add((method, input));
        if (RejectEverything && method.StartsWith("Set", StringComparison.Ordinal))
            return 1;
        switch (method)
        {
            case "GetGamingSysInfo" when input == 0:
                return SensorMask;
            case "GetGamingSysInfo" when input == AcerProtocol.BatteryStatusQuery:
                return BatteryBoost ? 1UL << 40 : 0;
            case "GetGamingSysInfo":
                return ((input >> 8) & 0xFF) switch
                {
                    1 when FailCpuTemp => 1,
                    1 => (ulong)(CpuTemp ?? 0) << 8,
                    10 => (ulong)GpuTemp << 8,
                    2 or 6 or 9 => 3000UL << 8,
                    4 or 8 => 2000UL << 8,
                    _ => 0,
                };
            case "SetGamingFanBehavior":
                foreach (var bit in Behavior.Keys.ToList())
                    if ((input & (1UL << bit)) != 0)
                        Behavior[bit] = (FanBehavior)((input >> (16 + 2 * bit)) & 3);
                return 0;
            case "SetGamingFanSpeed":
                Speed[(int)(input & 0xFF)] = (int)((input >> 8) & 0xFF);
                return 0;
            case "GetGamingFanBehavior":
                return Behavior.Where(b => (input & (1UL << b.Key)) != 0).Aggregate(0UL, (value, b) => value | ((ulong)b.Value << (2 * b.Key))) << 8;
            case "GetFunction" when input == AcerProtocol.DustDefenderQuery:
                return (SupportsDustDefender ? 1UL << 24 : 0) | (1UL << 16);
            case "GetFunction" when input == AcerProtocol.DustDefenderStatusQuery:
                return (DustDefenderRunning ? 1UL << 24 : 0) | (1UL << 16);
            case "SetFunction" when SupportsDustDefender && input == AcerProtocol.DustDefenderStartInput:
                if (DustDefenderBusy)
                    return AcerProtocol.DustDefenderBusy;
                DustDefenderRunning = true;
                return 0;
            case "GetGamingFanSpeed":
                return (ulong)Speed.GetValueOrDefault((int)input) << 8;
            case "GetGamingMiscSetting" when SupportsModes && input == 0x0A:
                return ModeMask << 8;
            case "GetGamingMiscSetting" when SupportsModes && input == 0x0B:
                return (ulong)Mode << 8;
            case "SetGamingMiscSetting" when SupportsModes && (input & 0xFF) == 0x0B:
                Mode = (OperatingMode)(input >> 8);
                return 0;
            case "GetFunction" when SupportsCoolBoost && input == AcerProtocol.CoolBoostGetInput:
                return (CoolBoost ? 1UL : 0UL) << 8;
            case "SetFunction" when (input & 0xFFFF) == 0x07:
                CoolBoost = ((input >> 16) & 1) == 1;
                return 0;
            case "GetFunction" when SupportsUsbCharging && input == AcerProtocol.UsbChargingQuery:
                return UsbCharging;
            case "SetFunction" when SupportsUsbCharging && (input & 0xFF) == 0x04:
                UsbCharging = input & ~0xFFUL;
                return 0;
            case "SetGamingFanTable" when SupportsFanTable:
                FanTable = (byte)input;
                return 0;
            case "GetGamingProfile" when Profile is { } profile:
                return profile;
            default:
                return 1;
        }
    }

    public void Dispose() { }
}

/// <summary>Runs firmware work at once on the caller's thread (instead of the control loop's).</summary>
internal sealed class ImmediateDispatcher(AcerDevice device) : IDeviceDispatcher
{
    public Task<T> InvokeAsync<T>(Func<AcerDevice, T> action) => Task.FromResult(action(device));
}

internal sealed class FakeLoad : ILoadMonitor
{
    public string? CpuName => "cpu";
    public string? GpuName => "gpu";
    public (double? Cpu, double? Gpu) Sample() => (10, 5);
    public void Dispose() { }
}

internal sealed class FakePower : IPowerSource
{
    public bool IsOnAcPower { get; set; } = true;
    public int? Percent { get; set; } = 80;
    public bool? Charging { get; set; }

    public PowerStatus Read() => new(IsOnAcPower, Percent, Charging);
}

/// <summary>Windows' power management: records what was held and changed.</summary>
internal sealed class FakeSystemPower : ISystemPower
{
    public static readonly Guid Scheme = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");

    public bool Awake { get; private set; }

    /// <summary>The power plan's settings: (subgroup, setting) → value, the same on AC and battery.</summary>
    public Dictionary<(Guid, Guid), uint> Values { get; } = new();

    public void HoldAwake() => Awake = true;

    public void ReleaseAwake() => Awake = false;

    public SavedPowerScheme? Override(IReadOnlyList<PowerOverride> changes)
    {
        var saved = changes.Select(o => new SavedPowerValue(o.Subgroup, o.Setting,
            Values.GetValueOrDefault((o.Subgroup, o.Setting), 1u), Values.GetValueOrDefault((o.Subgroup, o.Setting), 1u))).ToList();
        foreach (var o in changes)
            Values[(o.Subgroup, o.Setting)] = o.Value;
        return new SavedPowerScheme(Scheme, saved);
    }

    public bool Restore(SavedPowerScheme saved)
    {
        foreach (var value in saved.Values)
            Values[(value.Subgroup, value.Setting)] = value.Ac;
        return true;
    }
}

/// <summary>Firmware events raised by the test.</summary>
internal sealed class FakeFirmwareEvents : IFirmwareEvents
{
    public bool Available { get; set; } = true;
    public bool Started { get; private set; }

    public event Action<FirmwareEvent>? Raised;

    public bool Start() => Started = Available;

    public void Raise(params byte[] detail) => Raised?.Invoke(FirmwareEvent.Decode(detail)!);

    public void Dispose() => Started = false;
}

/// <summary>The GPU driver's sensor: counts reads, since reading a sleeping GPU could wake it.</summary>
internal sealed class FakeGpuSensor : ITemperatureSensor
{
    public double? Temperature { get; set; } = 62;
    public int? Limit { get; set; }
    public int Reads { get; private set; }
    public SensorStatus Status { get; } = new(ChipSensor.NvidiaDriver);

    public double? Read()
    {
        Reads++;
        return Temperature;
    }

    public void Dispose() { }
}

/// <summary>The CPU's own sensor, with the limit it reports (TjMax less any TCC offset).</summary>
internal sealed class FakeCpuSensor : ITemperatureSensor
{
    public double? Temperature { get; set; } = 60;
    public int? Limit { get; set; }
    public SensorStatus Status { get; } = new(ChipSensor.IntelPackage, 100);

    public double? Read() => Temperature;

    public void Dispose() { }
}

internal sealed class FakeGpuPowerState : IGpuPowerState
{
    public bool? On { get; set; }
    public bool? IsOn() => On;
}
