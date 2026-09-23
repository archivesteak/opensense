using OpenSense.Core.Hardware;

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

    /// <summary>The CPU temperature stopped reporting, so the fans were handed back to the firmware.</summary>
    FansHandedBack,

    LightingRejected,
    BacklightTimeoutRejected,
    WindowsKeyRejected,
    LcdOverdriveRejected,
}

/// <summary>Something the user should know about, e.g. the firmware rejected a change.</summary>
/// <param name="Detail">Technical detail shown as it is, such as an error message.</param>
/// <param name="Important">Worth interrupting the user for (failsafe), not just a banner in the window.</param>
public sealed record ControlNotice(NoticeKind Kind, string? Detail = null, bool Important = false)
{
    /// <summary>The mode the firmware refused, for <see cref="NoticeKind.OperatingModeRejected"/>.</summary>
    public OperatingMode? OperatingMode { get; init; }
}
