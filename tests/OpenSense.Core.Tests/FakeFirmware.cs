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

    /// <summary>Answers the CoolBoost query (APGeAction), as 2021-and-earlier Nitro firmware does.</summary>
    public bool SupportsCoolBoost { get; set; }
    public bool RejectEverything { get; set; }

    public Dictionary<int, FanBehavior> Behavior { get; } = new() { [0] = FanBehavior.Auto, [3] = FanBehavior.Auto };
    public Dictionary<int, int> Speed { get; } = new();
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

    public ulong Invoke(string className, string method, ulong input)
    {
        Calls.Add((method, input));
        if (RejectEverything && method.StartsWith("Set", StringComparison.Ordinal))
            return 1;
        switch (method)
        {
            case "GetGamingSysInfo" when input == 0:
                return 0x227UL << 24;
            case "GetGamingSysInfo":
                return ((input >> 8) & 0xFF) switch
                {
                    1 when FailCpuTemp => 1,
                    1 => (ulong)(CpuTemp ?? 0) << 8,
                    10 => (ulong)GpuTemp << 8,
                    2 or 6 => 3000UL << 8,
                    _ => 0,
                };
            case "SetGamingFanBehavior":
                foreach (var bit in new[] { 0, 3 })
                    if ((input & (1UL << bit)) != 0)
                        Behavior[bit] = (FanBehavior)((input >> (16 + 2 * bit)) & 3);
                return 0;
            case "SetGamingFanSpeed":
                Speed[(int)(input & 0xFF)] = (int)((input >> 8) & 0xFF);
                return 0;
            case "GetGamingFanBehavior":
                return ((((input & 0x1) != 0 ? (ulong)Behavior[0] : 0) | ((input & 0x8) != 0 ? (ulong)Behavior[3] << 6 : 0))) << 8;
            case "GetGamingFanSpeed":
                return (ulong)Speed.GetValueOrDefault((int)input) << 8;
            case "GetGamingMiscSetting" when SupportsModes && input == 0x0A:
                return 0x33UL << 8;
            case "GetGamingMiscSetting" when SupportsModes && input == 0x0B:
                return (ulong)Mode << 8;
            case "SetGamingMiscSetting" when SupportsModes && (input & 0xFF) == 0x0B:
                Mode = (OperatingMode)(input >> 8);
                return 0;
            case "GetFunction" when SupportsCoolBoost && input == AcerProtocol.CoolBoostGetInput:
                return (CoolBoost ? 1UL : 0UL) << 8;
            case "SetFunction":
                CoolBoost = ((input >> 16) & 1) == 1;
                return 0;
            default:
                return 1;
        }
    }

    public void Dispose() { }
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
}

/// <summary>The GPU driver's sensor: counts reads, since reading a sleeping GPU could wake it.</summary>
internal sealed class FakeGpuSensor : ITemperatureSensor
{
    public double? Temperature { get; set; } = 62;
    public int Reads { get; private set; }
    public SensorStatus Status { get; } = new(ChipSensor.NvidiaDriver);

    public double? Read()
    {
        Reads++;
        return Temperature;
    }

    public void Dispose() { }
}

internal sealed class FakeGpuPowerState : IGpuPowerState
{
    public bool? On { get; set; }
    public bool? IsOn() => On;
}
