namespace OpenSense.Core.Hardware;

/// <summary>Battery functions of <c>BatteryControl</c>, by their bit.</summary>
public enum BatteryFunction : byte
{
    /// <summary>Charging stops at 80 % to slow the battery's wear.</summary>
    HealthMode = 1,

    /// <summary>The firmware charges the battery full, runs it down and charges it again, to measure it anew.</summary>
    Calibration = 2,
}

/// <summary>What the battery supports and what is on.</summary>
public sealed record BatteryHealthStatus(bool HealthModeSupported, bool CalibrationSupported, bool HealthMode, bool Calibrating);

/// <summary>
/// Encoding of the <c>BatteryControl</c> WMI class, whose methods take named parameters. Every function is a bit:
/// the status lists supported functions as bits of <c>uFunctionList</c> and whether each is on in
/// <c>uFunctionStatus[bit index]</c> (1 = on). Measured on the AN515-57: both functions listed, health mode takes effect
/// at once. Pure functions.
/// </summary>
public static class BatteryProtocol
{
    /// <summary>The laptop's battery (0 is refused).</summary>
    public const byte BatteryNumber = 1;

    /// <summary>The query Acer's software asks the status with.</summary>
    public const byte StatusQuery = 1;

    public static WmiArgument[] StatusArguments() =>
    [
        new("uBatteryNo", BatteryNumber),
        new("uFunctionQuery", StatusQuery),
        new("uReserved", new byte[2]),
    ];

    public static WmiArgument[] SetArguments(BatteryFunction function, bool on) =>
    [
        new("uBatteryNo", BatteryNumber),
        new("uFunctionMask", (byte)function),
        new("uFunctionStatus", on ? (byte)1 : (byte)0),
        new("uReservedIn", new byte[5]),
    ];

    /// <summary>The status, or null when the firmware answered with an error.</summary>
    public static BatteryHealthStatus? DecodeStatus(WmiOutputs outputs)
    {
        if (outputs.Bytes("uReturn") is not { } result || result.Any(b => b != 0)
            || outputs.Value("uFunctionList") is not { } list || outputs.Bytes("uFunctionStatus") is not { } status)
            return null;
        bool Supported(BatteryFunction f) => (list & (byte)f) != 0;
        bool On(BatteryFunction f) => BitIndex(f) < status.Length && status[BitIndex(f)] == 1;
        return new BatteryHealthStatus(Supported(BatteryFunction.HealthMode), Supported(BatteryFunction.Calibration),
            On(BatteryFunction.HealthMode), On(BatteryFunction.Calibration));
    }

    public static bool SetSucceeded(WmiOutputs outputs) => outputs.Value("uReturn") == 0;

    private static int BitIndex(BatteryFunction function) => System.Numerics.BitOperations.TrailingZeroCount((uint)function);
}
