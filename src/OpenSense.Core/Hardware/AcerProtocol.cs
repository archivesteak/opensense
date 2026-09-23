namespace OpenSense.Core.Hardware;

/// <summary>
/// Bit-level encoding of the Acer gaming WMI interface. Pure functions.
/// </summary>
public static class AcerProtocol
{
    public const string GamingClass = "AcerGamingFunction";
    public const string ActionClass = "APGeAction";

    /// <summary>Bits 0-7 of every output are a status code; 0 is success.</summary>
    public static byte Status(ulong output) => (byte)(output & 0xFF);

    public static bool IsOk(ulong output) => Status(output) == 0;

    // --- sensors ---------------------------------------------------------------------------

    public const uint SupportedSensorsQuery = 0x00;

    public static uint SensorReadInput(SensorId id) => 0x01u | ((uint)id << 8);

    public static int SensorValue(ulong output) => (int)((output >> 8) & 0xFFFF);

    /// <summary>
    /// Sensors listed in a <see cref="SupportedSensorsQuery"/> answer: bit n of bits 24+ is sensor id n+1.
    /// (AN515-57 answers 0x227000000 → CPU temp, CPU fan, system temp, GPU fan, GPU temp.)
    /// </summary>
    public static IReadOnlyList<SensorId> DecodeSensorMask(ulong output)
    {
        var mask = output >> 24;
        return Enum.GetValues<SensorId>().Where(id => (mask & (1UL << ((int)id - 1))) != 0).ToList();
    }

    // --- fans ------------------------------------------------------------------------------

    public static ulong FanBehaviorInput(IEnumerable<(FanChannel Fan, FanBehavior Behavior)> settings)
    {
        ulong mask = 0, modes = 0;
        foreach (var (fan, behavior) in settings)
        {
            mask |= 1UL << fan.GroupBit;
            modes |= (ulong)behavior << (16 + 2 * fan.GroupBit);
        }
        return mask | modes;
    }

    public static ulong FanSpeedInput(FanChannel fan, int percent) =>
        fan.SpeedId | ((ulong)Math.Clamp(percent, 0, 100) << 8);

    /// <summary><c>GetGamingFanBehavior</c> takes the same fan mask as the setter.</summary>
    public static uint FanBehaviorQuery(FanChannel fan) => 1u << fan.GroupBit;

    /// <summary>Behaviour of <paramref name="fan"/> in a <c>GetGamingFanBehavior</c> answer (same layout as the setter, shifted down 8).</summary>
    public static FanBehavior? FanBehaviorValue(ulong output, FanChannel fan) =>
        ((output >> (8 + 2 * fan.GroupBit)) & 0x3) is var v and >= 1 and <= 3 ? (FanBehavior)v : null;

    /// <summary><c>GetGamingFanSpeed</c> takes the speed id and answers the current duty in bits 8-15.</summary>
    public static int FanDutyValue(ulong output) => (int)((output >> 8) & 0xFF);

    // --- CoolBoost (APGeAction) ------------------------------------------------------------

    public const uint CoolBoostGetInput = 0x0207;

    public static ulong CoolBoostSetInput(bool on) => 0x07UL | ((on ? 1UL : 0UL) << 16);

    public static bool CoolBoostValue(ulong output) => ((output >> 8) & 0xFF) == 1;

    // --- misc settings (operating mode, GPU mode) ------------------------------------------

    public static uint MiscGetInput(MiscSetting setting) => (uint)setting;

    public static ulong MiscSetInput(MiscSetting setting, byte value) => (ulong)setting | ((ulong)value << 8);

    public static byte MiscValue(ulong output) => (byte)((output >> 8) & 0xFF);

    /// <summary>Operating modes whose bit is set in a supported-modes mask (bit n = mode value n).</summary>
    public static IReadOnlyList<OperatingMode> DecodeOperatingModeMask(ulong output)
    {
        var mask = (output >> 8) & 0xFFFF;
        return Enum.GetValues<OperatingMode>()
            .Where(m => (mask & (1UL << (int)m)) != 0)
            .ToList();
    }
}
