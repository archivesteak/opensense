namespace OpenSense.Core.Hardware;

/// <summary>
/// Bit-level encoding of the Acer gaming WMI interface. Pure functions.
/// </summary>
public static class AcerProtocol
{
    public const string GamingClass = "AcerGamingFunction";
    public const string ActionClass = "APGeAction";

    /// <summary>Battery charge limit and calibration; its methods take named parameters (see <see cref="BatteryProtocol"/>).</summary>
    public const string BatteryClass = "BatteryControl";

    /// <summary>The firmware's event class (see <see cref="FirmwareEvent"/>).</summary>
    public const string EventClass = "APGeEvent";

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

    /// <summary>Each fan's behaviour sits at bits <c>16 + 2 × group bit</c> (CPU 16–17, GPU 22–23, GPU 2 24–25).</summary>
    public static ulong FanBehaviorInput(IEnumerable<(FanChannel Fan, FanBehavior Behavior)> settings)
    {
        ulong mask = 0, modes = 0;
        foreach (var (fan, behavior) in settings)
        {
            var bit = GroupBit(fan);
            mask |= 1UL << bit;
            modes |= (ulong)behavior << (16 + 2 * bit);
        }
        return mask | modes;
    }

    /// <summary>
    /// <c>SetGamingFanSpeed</c> for a fan on Custom. The percentage is not a speed but a boost over Auto:
    /// the firmware keeps its own curve and moves the fan that far towards full speed (0 % = Auto,
    /// 100 % = Max; measured on the AN515-57, where Custom 40 % turned Auto's 3890 rpm into 4690 of 5880).
    /// </summary>
    public static ulong FanSpeedInput(FanChannel fan, int percent) =>
        SpeedId(fan) | ((ulong)Math.Clamp(percent, 0, 100) << 8);

    /// <summary>
    /// The boost's resolution. The embedded controller counts it in whole tens and drops the rest (AN515-57 firmware:
    /// 0–9 % boosts nothing, 95 % acts as 90 %), and Acer's software only sends multiples of 10.
    /// </summary>
    public const int FanSpeedStep = 10;

    /// <summary>The boost the firmware can do that is nearest to <paramref name="percent"/>.</summary>
    public static int NearestFanSpeed(int percent) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) / (double)FanSpeedStep, MidpointRounding.AwayFromZero) * FanSpeedStep;

    /// <summary><c>GetGamingFanBehavior</c> takes the same fan mask as the setter.</summary>
    public static uint FanBehaviorQuery(FanChannel fan) => 1u << GroupBit(fan);

    /// <summary>Behaviour of <paramref name="fan"/> in a <c>GetGamingFanBehavior</c> answer (same layout as the setter, shifted down 8).</summary>
    public static FanBehavior? FanBehaviorValue(ulong output, FanChannel fan) =>
        ((output >> (8 + 2 * GroupBit(fan))) & 0x3) is var v and >= 1 and <= 3 ? (FanBehavior)v : null;

    /// <summary><c>GetGamingFanSpeed</c> takes the speed id.</summary>
    public static uint FanBoostQuery(FanChannel fan) => SpeedId(fan);

    private static int GroupBit(FanChannel fan) =>
        fan.GroupBit ?? throw new ArgumentException($"The {fan.Name} fan can't be controlled.", nameof(fan));

    private static byte SpeedId(FanChannel fan) =>
        fan.SpeedId ?? throw new ArgumentException($"The {fan.Name} fan can't be controlled.", nameof(fan));

    /// <summary>
    /// <c>GetGamingFanSpeed</c> takes the speed id and answers, in bits 8-15, the last percentage written with
    /// <c>SetGamingFanSpeed</c>. It is not what the fan is doing: on Auto it still reads the old Custom value.
    /// </summary>
    public static int FanBoostValue(ulong output) => (int)((output >> 8) & 0xFF);

    // --- fan table ---------------------------------------------------------------------------

    /// <summary><c>SetGamingFanTable</c>: the table in byte 0 (the AN515-57 V1.17 keeps only that byte, in the embedded controller).</summary>
    public static ulong FanTableInput(FanTable table) => (byte)table;

    /// <summary>The table in a <c>GetGamingFanTable</c> answer, byte 1; null for 0 (none picked yet) or a value OpenSense doesn't know.</summary>
    public static FanTable? FanTableValue(ulong output) =>
        (byte)(output >> 8) is var value && Enum.IsDefined((FanTable)value) ? (FanTable)value : null;

    /// <summary>
    /// The models NitroSense (3.01) sets a fan table on. It sends one only with its GPU overclock level, which runs where
    /// the model's configuration lists its GPUs: these six, and a catch-all "ANX" configuration that no model name
    /// matches. Other firmware answers the calls too, but the AN515-57's never reads the value back.
    /// </summary>
    private static readonly string[] FanTableModels = ["AN515-46", "AN515-47", "AN515-58", "AN517-42", "AN517-43", "AN517-55"];

    /// <param name="model">The laptop's model name, e.g. "Acer Nitro AN515-58".</param>
    public static bool UsesFanTable(string? model) =>
        model is not null && FanTableModels.Any(m => model.Contains(m, StringComparison.OrdinalIgnoreCase));

    // --- CoolBoost (APGeAction) ------------------------------------------------------------

    public const uint CoolBoostGetInput = 0x0207;

    public static ulong CoolBoostSetInput(bool on) => 0x07UL | ((on ? 1UL : 0UL) << 16);

    public static bool CoolBoostValue(ulong output) => ((output >> 8) & 0xFF) == 1;

    // --- Dust Defender (APGeAction function 7, sub-functions 1 and 3) ------------------------

    /// <summary>Answers, in byte 3, whether the model has Dust Defender (AN515-57: 0x10000, no).</summary>
    public const uint DustDefenderQuery = 0x0107;

    /// <summary>Answers, in byte 3, whether a cleaning run is going.</summary>
    public const uint DustDefenderStatusQuery = 0x0307;

    /// <summary>Starts a cleaning run; there is no command to stop one.</summary>
    public const ulong DustDefenderStartInput = 0x0107;

    /// <summary>The status of a start the embedded controller turned down for now (Acer: "EC bypass request").</summary>
    public const byte DustDefenderBusy = 0xE5;

    /// <summary>Byte 3 of a <see cref="DustDefenderQuery"/> or <see cref="DustDefenderStatusQuery"/> answer.</summary>
    public static bool DustDefenderValue(ulong output) => ((output >> 24) & 0xFF) == 1;

    // --- power-off USB charging (APGeAction function 4) ------------------------------------

    public const uint UsbChargingQuery = 0x04;

    /// <summary>Battery levels at which charging over USB stops while the laptop is off.</summary>
    public static IReadOnlyList<int> UsbChargingFloors { get; } = [10, 20, 30];

    /// <summary>The floor Acer's software starts from.</summary>
    public const int DefaultUsbChargingFloor = 30;

    private const byte UsbChargingOn = 0x0F, UsbChargingOff = 0x1F;

    public static ulong UsbChargingInput(bool on, int floor) =>
        UsbChargingQuery | ((ulong)(on ? UsbChargingOn : UsbChargingOff) << 8) | ((ulong)floor << 16);

    /// <summary>
    /// Only state 0x0F is on. The floor is null when the firmware has none (the AN515-57 answers 0x6400 until it is
    /// first set: state 0x64, no floor).
    /// </summary>
    public static UsbChargingState UsbChargingValue(ulong output) => new(
        ((output >> 8) & 0xFF) == UsbChargingOn,
        (int)((output >> 16) & 0xFF) is var floor && UsbChargingFloors.Contains(floor) ? floor : null);

    // --- battery status (GetGamingSysInfo 0x02) ---------------------------------------------

    public const uint BatteryStatusQuery = 0x02;

    /// <summary>
    /// The battery-boost flag, byte 5: 1 while the battery can add to the adapter's power (AN515-57 on AC at 100 %).
    /// Acer's software allows the performance modes only on AC with the flag set.
    /// </summary>
    public static bool BatteryBoostValue(ulong output) => ((output >> 40) & 0xFF) == 1;

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
