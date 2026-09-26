using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Hardware;

/// <summary>
/// Where the operating mode is read and set: the gaming WMI interface (misc setting 0x0B) on every model so far, the
/// embedded controller's HID interface on 2024+ models that have one.
/// </summary>
public interface IOperatingModeChannel
{
    OperatingMode? Read();

    bool Write(OperatingMode mode);
}

/// <summary>The operating mode through <c>Get/SetGamingMiscSetting(0x0B)</c>.</summary>
public sealed class WmiOperatingModeChannel(AcerDevice device) : IOperatingModeChannel
{
    public OperatingMode? Read() => device.GetOperatingMode();

    public bool Write(OperatingMode mode) => device.SetOperatingMode(mode);
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
        device.SetOperatingMode(mode);
        return true;
    }
}
