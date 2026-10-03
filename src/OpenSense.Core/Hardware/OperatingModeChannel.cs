using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Hardware;

/// <summary>
/// Where the operating mode is read and set: the gaming WMI interface (misc setting 0x0B) on most models, the embedded
/// controller's HID interface on 2024+ models that have one, <c>APGeAction</c> function 7 on the NL16-71G.
/// </summary>
public interface IOperatingModeChannel
{
    OperatingMode? Read();

    bool Write(OperatingMode mode);

    /// <summary>The embedded controller's HID interface where it sets the modes, else function 7 where the modes are its, else misc 0x0B.</summary>
    static IOperatingModeChannel For(AcerDevice device, DeviceCapabilities capabilities) =>
        capabilities.EcHid is { Modes.Count: > 0 } ecHid && device.EcHid is { } hid ? new EcHidOperatingModeChannel(device, hid, ecHid.Modes)
        : capabilities.ActionOperatingModes ? new ActionOperatingModeChannel(device)
        : new WmiOperatingModeChannel(device);
}

/// <summary>The operating mode through <c>Get/SetGamingMiscSetting(0x0B)</c>.</summary>
public sealed class WmiOperatingModeChannel(AcerDevice device) : IOperatingModeChannel
{
    public OperatingMode? Read() => device.GetOperatingMode();

    public bool Write(OperatingMode mode) => device.SetOperatingMode(mode);
}

/// <summary>
/// The operating mode through <c>APGeAction</c> function 7, where its own answer says it is the mode and misc 0x0B has
/// none (<see cref="DeviceCapabilities.ActionOperatingModes"/>: the NL16-71G, which is back in Performance after every
/// boot until a mode is written).
/// </summary>
public sealed class ActionOperatingModeChannel(AcerDevice device) : IOperatingModeChannel
{
    public OperatingMode? Read() => device.GetActionOperatingMode();

    public bool Write(OperatingMode mode) => device.SetActionOperatingMode(mode);
}

/// <summary>
/// The operating mode through the embedded controller's HID interface, by the values its mode capability gives
/// <paramref name="modes"/> (<see cref="EcHidProtocol.ModeValue"/>). As with Acer's software there, a write goes to
/// misc <c>0x0B</c> as well, after the controller has taken it.
/// </summary>
public sealed class EcHidOperatingModeChannel(AcerDevice device, EcHidDevice hid, IReadOnlyList<OperatingMode> modes) : IOperatingModeChannel
{
    public OperatingMode? Read() => hid.ReadMode() is { } value ? EcHidProtocol.ModeFromValue(modes, value) : null;

    public bool Write(OperatingMode mode)
    {
        if (EcHidProtocol.ModeValue(modes, mode) is not { } value || !hid.WriteMode(value))
            return false;
        // The controller's answer is the one that counts; the WMI copy keeps the firmware's own view in step.
        device.SynchronizeHidOperatingMode(mode);
        return true;
    }
}
