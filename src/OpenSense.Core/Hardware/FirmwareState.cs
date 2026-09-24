using OpenSense.Core.Control;

namespace OpenSense.Core.Hardware;

/// <summary>What the firmware is currently set to (e.g. by NitroSense before OpenSense was installed).</summary>
public sealed record FirmwareState(
    IReadOnlyDictionary<FanId, FanBehavior?> FanBehaviors,
    IReadOnlyDictionary<FanId, int?> FanBoosts,
    OperatingMode? OperatingMode,
    bool? CoolBoost,
    GpuMode? GpuMode)
{
    public static FirmwareState Read(AcerDevice device, DeviceCapabilities caps) => new(
        caps.Fans.ToDictionary(f => f.Id, f => device.GetFanBehavior(f)),
        caps.Fans.ToDictionary(f => f.Id, f => device.GetFanBoost(f)),
        caps.HasOperatingModes ? device.GetOperatingMode() : null,
        caps.CoolBoost ? device.GetCoolBoost() : null,
        caps.GpuModeSwitch ? device.GetGpuMode() : null);

    /// <summary>
    /// A profile that keeps the machine as it is, so the first launch of OpenSense changes nothing.
    /// </summary>
    public ControlProfile ToProfile(ControlProfile defaults)
    {
        var behaviors = FanBehaviors.Values.Where(b => b is not null).Select(b => b!.Value).ToList();
        var mode = behaviors switch
        {
            _ when behaviors.Contains(FanBehavior.Custom) => FanControlMode.Custom,
            _ when behaviors.Count > 0 && behaviors.All(b => b == FanBehavior.Max) => FanControlMode.Max,
            _ => FanControlMode.Auto,
        };
        // A fan left on Auto next to a Custom one is a boost of 0 %, one on Max a boost of 100 %.
        var manual = FanBehaviors.ToDictionary(
            kv => kv.Key,
            kv => new ManualFanSetting(kv.Value switch
            {
                FanBehavior.Custom => FanBoosts.GetValueOrDefault(kv.Key) ?? defaults.ManualFor(kv.Key).Percent,
                FanBehavior.Max => 100,
                _ => 0,
            }));

        return defaults with
        {
            Mode = mode,
            Manual = mode == FanControlMode.Custom ? manual : defaults.Manual,
            OperatingMode = OperatingMode ?? defaults.OperatingMode,
            CoolBoost = CoolBoost ?? defaults.CoolBoost,
        };
    }
}
