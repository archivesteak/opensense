using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Control;

/// <summary>What a <see cref="ControlNotice"/> reports. The engine sends the kind; the app words it in the user's language.</summary>
public enum NoticeKind
{
    /// <summary>The firmware interface refused access: the engine is not running with administrator rights.</summary>
    FirmwareAccessDenied,

    /// <summary>The control loop hit an unexpected error (see <see cref="ControlNotice.Detail"/>).</summary>
    ControlLoopError,

    /// <summary>The firmware refused <see cref="ControlNotice.OperatingMode"/>.</summary>
    OperatingModeRejected,

    CoolBoostRejected,

    /// <summary>The firmware refused the fan curve (<see cref="ControlProfile.FanTable"/>).</summary>
    FanTableRejected,

    /// <summary>The CPU temperature stopped reporting, so the fans were handed back to the firmware.</summary>
    FansHandedBack,

    LightingRejected,
    BacklightTimeoutRejected,
    WindowsKeyRejected,
    LcdOverdriveRejected,

    ChargeLimitRejected,
    UsbChargingRejected,

    /// <summary>The battery calibration finished.</summary>
    CalibrationFinished,

    /// <summary>The laptop slept or shut down during the calibration, which ends it.</summary>
    CalibrationInterrupted,

    /// <summary>The adapter was unplugged, so the calibration was stopped.</summary>
    CalibrationStoppedUnplugged,

    /// <summary>The graphics driver refused the GPU clock offsets.</summary>
    GpuClocksRejected,

    /// <summary>The Mode key switched to <see cref="ControlNotice.OperatingMode"/> (a toast, like Acer's on-screen display).</summary>
    OperatingModeSwitchedByKey,

    /// <summary>The Mode key is set to Turbo, which needs the adapter and a charged battery.</summary>
    TurboUnavailable,

    /// <summary>
    /// The power supply changed which mode runs (see <see cref="ControlNotice.PowerLimit"/>): now
    /// <see cref="ControlNotice.OperatingMode"/>.
    /// </summary>
    OperatingModeChangedByPower,
}

/// <summary>Something the user should know about, e.g. the firmware rejected a change.</summary>
/// <param name="Detail">Technical detail shown as it is, such as an error message.</param>
/// <param name="Important">Worth interrupting the user for (failsafe), not just a banner in the window.</param>
public sealed record ControlNotice(NoticeKind Kind, string? Detail = null, bool Important = false)
{
    /// <summary>The mode the firmware refused, or the one now running.</summary>
    public OperatingMode? OperatingMode { get; init; }

    /// <summary>What the power supply allows, for the operating-mode notices.</summary>
    public PowerLimit? PowerLimit { get; init; }

    /// <summary>The light that rejected a change (<see cref="NoticeKind.LightingRejected"/>).</summary>
    public LightingLocation? Light { get; init; }
}
