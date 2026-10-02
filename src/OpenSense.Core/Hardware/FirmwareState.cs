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
    /// <summary>Whether charging stops at 80 %; follows later changes.</summary>
    public bool? ChargeLimit { get; init; }

    /// <summary>Power-off USB charging; follows later changes.</summary>
    public UsbChargingState? UsbCharging { get; init; }

    /// <summary>The boot animation and sound; follows later changes.</summary>
    public bool? BootAnimation { get; init; }

    /// <summary>The embedded controller's fan curve; null when none has been picked.</summary>
    public FanTable? FanTable { get; init; }

    public static FirmwareState Read(AcerDevice device, DeviceCapabilities caps) => new(
        caps.ControllableFans.ToDictionary(f => f.Id, f => device.GetFanBehavior(f)),
        caps.ControllableFans.ToDictionary(f => f.Id, f => device.GetFanBoost(f)),
        caps.HasOperatingModes ? ReadOperatingMode(device, caps) : null,
        caps.CoolBoost ? device.GetCoolBoost() : null,
        caps.GpuModeSwitch ? device.GetGpuMode() : null)
    {
        ChargeLimit = caps.Battery.ChargeLimit ? device.GetBatteryHealth()?.HealthMode : null,
        UsbCharging = caps.UsbCharging ? device.GetUsbCharging() : null,
        BootAnimation = caps.BootAnimation ? device.GetBootAnimation() : null,
        FanTable = caps.FanTable ? device.GetFanTable() : null,
    };

    private static OperatingMode? ReadOperatingMode(AcerDevice device, DeviceCapabilities caps) =>
        device.EcHid is { } hid && caps.EcHid is { Modes.Count: > 0 } ec
            ? new EcHidOperatingModeChannel(device, hid, ec.Modes).Read()
            : device.GetOperatingMode();

    /// <summary>
    /// Adopts readable firmware settings. Unreadable custom fan boosts retain the configured profile
    /// (Auto on a fresh installation), rather than fabricating a speed.
    /// </summary>
    public ControlProfile ToProfile(ControlProfile defaults)
    {
        var behaviors = FanBehaviors.Values.Where(b => b is not null).Select(b => b!.Value).ToList();
        // A broken/unsupported custom-speed getter cannot supply a saved boost. Retain the configured fan profile
        // (Auto on a fresh install) instead of presenting a guessed percentage as an adopted firmware setting.
        var canAdoptFans = !FanBehaviors.Any(kv => kv.Value == FanBehavior.Custom && FanBoosts.GetValueOrDefault(kv.Key) is null);
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
            Mode = canAdoptFans ? mode : defaults.Mode,
            Manual = canAdoptFans && mode == FanControlMode.Custom ? manual : defaults.Manual,
            OperatingMode = OperatingMode ?? defaults.OperatingMode,
            CoolBoost = CoolBoost ?? defaults.CoolBoost,
            FanTable = FanTable ?? defaults.FanTable,
        };
    }
}
