using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace OpenSense.Core.Hardware;

/// <summary>Current refresh rate and supported progressive rates at the active resolution of one display.</summary>
public sealed record DisplayRefreshInfo(string DeviceName, uint CurrentHz, IReadOnlyList<uint> SupportedHz);

public interface IDisplayRefreshBackend
{
    IReadOnlyList<DisplayRefreshInfo> GetDisplays();
    bool SetRefreshRate(string deviceName, uint refreshRateHz);
}

/// <summary>What <see cref="DisplayRefreshController.Reconcile"/> did.</summary>
public enum DisplayRefreshOutcome
{
    /// <summary>Nothing to do: disabled, already matching the power source, or retries paused after repeated failures.</summary>
    Skipped,

    /// <summary>Every switchable display now runs at the rate for the current power source.</summary>
    Applied,

    /// <summary>Windows refused at least one mode change (e.g. while the lock screen is up).</summary>
    Failed,
}

/// <summary>Outcome of a refresh-rate check, including the target Hz when any display actually changed rate.</summary>
public readonly record struct DisplayRefreshResult(DisplayRefreshOutcome Outcome, bool OnAc, uint? SwitchedHz);

/// <summary>Refresh-rate selection rules: 60 Hz on battery, highest supported rate on AC power.</summary>
public static class DisplayRefreshPolicy
{
    public const uint BatteryTargetHz = 60;
    public const uint BatteryFallbackHz = 59; // Windows reports 59.94 Hz modes as integer 59 in DEVMODEW.dmDisplayFrequency

    public static uint? ChooseTargetFrequency(IReadOnlyList<uint> supportedHz, bool onAc)
    {
        if (supportedHz.Count == 0)
            return null;
        if (onAc)
            return supportedHz.Max();
        if (supportedHz.Contains(BatteryTargetHz))
            return BatteryTargetHz;
        return supportedHz.Contains(BatteryFallbackHz) ? BatteryFallbackHz : null;
    }

    public static bool CanSwitchBetweenBatteryAndAc(IReadOnlyList<uint> supportedHz) =>
        ChooseTargetFrequency(supportedHz, onAc: false) is { } batteryHz
        && ChooseTargetFrequency(supportedHz, onAc: true) is { } acHz
        && acHz > batteryHz;

    public static bool CanSwitchAnyDisplay(IReadOnlyList<DisplayRefreshInfo> displays) =>
        displays.Any(d => CanSwitchBetweenBatteryAndAc(d.SupportedHz));
}

/// <summary>Enumerates active desktop displays and updates their refresh rates through Win32 GDI.</summary>
public sealed class WindowsDisplayRefreshBackend : IDisplayRefreshBackend
{
    private const uint DmInterlaced = 0x00000002;
    private const int MaxEnumDevices = 64;
    private const int MaxEnumModes = 4096;

    public unsafe IReadOnlyList<DisplayRefreshInfo> GetDisplays()
    {
        var displays = new List<DisplayRefreshInfo>();
        for (uint devIndex = 0; devIndex < MaxEnumDevices; devIndex++)
        {
            var device = new DISPLAY_DEVICEW { cb = (uint)sizeof(DISPLAY_DEVICEW) };
            if (!PInvoke.EnumDisplayDevices(null, devIndex, ref device, 0))
                break;
            if ((device.StateFlags & DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                continue;

            var name = device.DeviceName.ToString();
            if (string.IsNullOrEmpty(name))
                continue;

            var current = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
            if (!PInvoke.EnumDisplaySettings(name, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref current)
                || current.dmDisplayFrequency == 0)
                continue;

            var rates = EnumerateSupportedRates(name, in current);
            if (rates.Length > 0)
                displays.Add(new DisplayRefreshInfo(name, current.dmDisplayFrequency, rates));
        }
        return displays;
    }

    public unsafe bool SetRefreshRate(string deviceName, uint refreshRateHz)
    {
        var mode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        if (!PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode))
            return false;
        if (mode.dmDisplayFrequency == refreshRateHz)
            return true;

        mode.dmDisplayFrequency = refreshRateHz;
        mode.dmFields = DEVMODE_FIELD_FLAGS.DM_DISPLAYFREQUENCY;

        fixed (char* name = deviceName)
        {
            var result = PInvoke.ChangeDisplaySettingsEx(name, &mode, HWND.Null, CDS_TYPE.CDS_UPDATEREGISTRY, null);
            if (result == DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
                return true;
            // Some restricted sessions allow transient dynamic switching even when registry writes fail.
            return PInvoke.ChangeDisplaySettingsEx(name, &mode, HWND.Null, 0, null) == DISP_CHANGE.DISP_CHANGE_SUCCESSFUL;
        }
    }

    private static unsafe uint[] EnumerateSupportedRates(string deviceName, in DEVMODEW current)
    {
        var rates = new SortedSet<uint>();
        var checkOrientation = (current.dmFields & DEVMODE_FIELD_FLAGS.DM_DISPLAYORIENTATION) != 0;
        var currentOrientation = current.Anonymous1.Anonymous2.dmDisplayOrientation;

        for (uint modeNum = 0; modeNum < MaxEnumModes; modeNum++)
        {
            var mode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
            if (!PInvoke.EnumDisplaySettings(deviceName, (ENUM_DISPLAY_SETTINGS_MODE)modeNum, ref mode))
                break;

            if (mode.dmPelsWidth != current.dmPelsWidth
                || mode.dmPelsHeight != current.dmPelsHeight
                || mode.dmBitsPerPel != current.dmBitsPerPel
                || (mode.dmFields & DEVMODE_FIELD_FLAGS.DM_DISPLAYFREQUENCY) == 0
                || mode.dmDisplayFrequency == 0
                || (mode.dmDisplayFlags & DmInterlaced) != 0)
                continue;

            if (checkOrientation
                && (mode.dmFields & DEVMODE_FIELD_FLAGS.DM_DISPLAYORIENTATION) != 0
                && mode.Anonymous1.Anonymous2.dmDisplayOrientation != currentOrientation)
                continue;

            rates.Add(mode.dmDisplayFrequency);
        }

        return [.. rates];
    }
}

/// <summary>
/// Coordinates power-source transitions with display refresh rate changes without overwriting manual tweaks
/// while staying on the same power source, and bounds retries when Windows temporarily blocks mode changes.
/// </summary>
public sealed class DisplayRefreshController(IDisplayRefreshBackend backend, IPowerSource power)
{
    internal const int MaxConsecutiveFailures = 3;

    private readonly object _gate = new();
    private readonly object _applyGate = new();

    private bool _enabled;
    private bool? _appliedOnAc;
    private int _failures;
    private bool? _failurePowerState;

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
                return _enabled;
        }
    }

    internal bool? AppliedOnAc
    {
        get
        {
            lock (_gate)
                return _appliedOnAc;
        }
    }

    /// <summary>Read-only enumeration; not serialized with <see cref="Reconcile"/> so the UI never waits on a mode switch.</summary>
    public bool IsSwitchAvailable => DisplayRefreshPolicy.CanSwitchAnyDisplay(backend.GetDisplays());

    public bool NeedsReconcile(bool onAc)
    {
        lock (_gate)
        {
            if (!_enabled || _appliedOnAc == onAc)
                return false;
            return !(_failurePowerState == onAc && _failures >= MaxConsecutiveFailures);
        }
    }

    public DisplayRefreshResult SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (_enabled == enabled)
                return new(DisplayRefreshOutcome.Skipped, _appliedOnAc ?? false, null);
            _enabled = enabled;
            ResetStateLocked();
        }
        return enabled
            ? Reconcile(forceReapply: true)
            : new(DisplayRefreshOutcome.Skipped, false, null);
    }

    /// <summary>Resets failure throttling after desktop unlock (Winlogon desktop rejects mode switches while locked).</summary>
    public bool OnDesktopUnlocked()
    {
        lock (_gate)
        {
            _failures = 0;
            _failurePowerState = null;
            return _enabled;
        }
    }

    /// <summary>Clears cached power state after sleep/resume so display refresh rate is re-verified.</summary>
    public bool OnPowerResume()
    {
        lock (_gate)
        {
            ResetStateLocked();
            return _enabled;
        }
    }

    public DisplayRefreshResult Reconcile(bool forceReapply = false)
    {
        lock (_applyGate)
        {
            if (!TryStartReconcile(forceReapply, out var onAc))
                return new(DisplayRefreshOutcome.Skipped, onAc, null);

            var allSucceeded = TryApplyDisplayRates(onAc, out var switchedHz);
            return FinishReconcile(onAc, allSucceeded, switchedHz);
        }
    }

    private void ResetStateLocked()
    {
        _appliedOnAc = null;
        _failures = 0;
        _failurePowerState = null;
    }

    private bool TryStartReconcile(bool forceReapply, out bool onAc)
    {
        lock (_gate)
        {
            if (!_enabled)
            {
                onAc = false;
                return false;
            }
        }

        onAc = power.Read().OnAc;

        lock (_gate)
        {
            if (!_enabled)
                return false;
            if (!forceReapply && _appliedOnAc == onAc)
                return false;
            return forceReapply || _failurePowerState != onAc || _failures < MaxConsecutiveFailures;
        }
    }

    private bool TryApplyDisplayRates(bool onAc, out uint? switchedHz)
    {
        var allSucceeded = true;
        switchedHz = null;

        foreach (var display in backend.GetDisplays())
        {
            if (!DisplayRefreshPolicy.CanSwitchBetweenBatteryAndAc(display.SupportedHz))
                continue;

            var target = DisplayRefreshPolicy.ChooseTargetFrequency(display.SupportedHz, onAc);
            if (target is null || target == display.CurrentHz)
                continue;

            if (backend.SetRefreshRate(display.DeviceName, target.Value))
                switchedHz = switchedHz is { } prev ? Math.Max(prev, target.Value) : target.Value;
            else
                allSucceeded = false;
        }

        return allSucceeded;
    }

    private DisplayRefreshResult FinishReconcile(bool onAc, bool allSucceeded, uint? switchedHz)
    {
        lock (_gate)
        {
            if (!_enabled)
                return new(DisplayRefreshOutcome.Skipped, onAc, null);

            if (allSucceeded)
            {
                _appliedOnAc = onAc;
                _failures = 0;
                _failurePowerState = null;
                return new(DisplayRefreshOutcome.Applied, onAc, switchedHz);
            }

            _appliedOnAc = null;
            _failures = _failurePowerState == onAc ? _failures + 1 : 1;
            _failurePowerState = onAc;
            return new(DisplayRefreshOutcome.Failed, onAc, null);
        }
    }
}
