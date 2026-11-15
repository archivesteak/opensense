using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;

namespace OpenSense.App.Services;

/// <summary>
/// The laptop's NitroSense key, read the way Acer's agent (PSAdminAgent) reads it to open NitroSense:
/// <list type="bullet">
/// <item>keyboard scan code E0 75 with no virtual key, so nothing else reacts to it (measured on the AN515-57);</item>
/// <item>on models that send it through Acer's vendor HID collection (usage page 0x88), a report starting 81 FF.</item>
/// </list>
/// Raw input registered for background delivery to a message-only window on the UI thread; no administrator
/// rights needed. Raises <see cref="Pressed"/> once per press, on the UI thread.
/// </summary>
public sealed unsafe partial class NitroSenseKey : IDisposable
{
    private const ushort GenericDesktopPage = 0x01, KeyboardUsage = 0x06;
    private const ushort AcerGamingPage = 0x88, AcerGamingUsage = 0x01;
    private const string WindowClass = "OpenSense.NitroSenseKey";
    private const int MaxRawInputSize = 65536;

    private readonly ILogger<NitroSenseKey> _log;
    private readonly DispatcherQueue _dispatcher;
    private readonly NitroSenseKeyTracker _tracker;

    private MessageWindow? _window;
    private bool _registeredKeyboard;
    private bool _registeredHid;
    private bool _systemEventsHooked;
    private bool _disposed;

    public NitroSenseKey(ILogger<NitroSenseKey> log, DispatcherQueue dispatcher)
    {
        _log = log;
        _dispatcher = dispatcher;
        _tracker = new NitroSenseKeyTracker(dispatch: action => _dispatcher.TryEnqueue(() => action()));
    }

    public event Action? Pressed;

    public bool IsListening => _window is not null && (_registeredKeyboard || _registeredHid);

    /// <summary>Starts listening; call on the UI thread (its message loop delivers the input).</summary>
    public void Start()
    {
        if (_disposed)
            return;

        HookSystemEvents();
        _tracker.Enable();
        _tracker.ResetRetries();
        EnsureStarted();
    }

    public void Stop()
    {
        UnhookSystemEvents();
        _tracker.Disable();
        CloseWindowAndUnregister();
    }

    private void EnsureStarted()
    {
        if (_disposed || !_tracker.IsEnabled)
            return;

        var wasListening = IsListening;
        if (_window is null)
        {
            _window = MessageWindow.Create(WindowClass, OnMessage, out var failure);
            if (_window is null)
            {
                LogFailed(failure.Call, failure.Error);
                ScheduleRetry();
                return;
            }
        }

        RegisterDevices();

        if (!IsListening)
        {
            _window.Dispose();
            _window = null;
            ScheduleRetry();
            return;
        }

        if (!wasListening)
            LogListening();

        if (_registeredKeyboard && _registeredHid)
            _tracker.CancelRetry();
        else
            ScheduleRetry();
    }

    private void RegisterDevices()
    {
        if (_window is null)
            return;

        var keyboard = SinkDevice(GenericDesktopPage, KeyboardUsage, _window.Handle);
        var hid = SinkDevice(AcerGamingPage, AcerGamingUsage, _window.Handle);

        if (!_registeredKeyboard && !_registeredHid)
        {
            RAWINPUTDEVICE[] both = [keyboard, hid];
            if (PInvoke.RegisterRawInputDevices(both, (uint)sizeof(RAWINPUTDEVICE)))
            {
                _registeredKeyboard = true;
                _registeredHid = true;
                return;
            }
        }

        // Fall back per device if early logon/desktop transition causes one collection to reject registration.
        int? lastError = null;
        if (!_registeredKeyboard)
        {
            if (RegisterSingle(keyboard, out var error))
                _registeredKeyboard = true;
            else
                lastError = error;
        }
        if (!_registeredHid)
        {
            if (RegisterSingle(hid, out var error))
                _registeredHid = true;
            else
                lastError = error;
        }

        if (lastError is { } failedError)
            LogFailed("RegisterRawInputDevices", failedError);
    }

    private static RAWINPUTDEVICE SinkDevice(ushort usagePage, ushort usage, HWND hwndTarget) => new()
    {
        usUsagePage = usagePage,
        usUsage = usage,
        dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK,
        hwndTarget = hwndTarget,
    };

    private static bool RegisterSingle(RAWINPUTDEVICE device, out int error)
    {
        RAWINPUTDEVICE[] single = [device];
        if (PInvoke.RegisterRawInputDevices(single, (uint)sizeof(RAWINPUTDEVICE)))
        {
            error = 0;
            return true;
        }
        error = Marshal.GetLastPInvokeError();
        return false;
    }

    private void CloseWindowAndUnregister()
    {
        if (_window is null)
        {
            _registeredKeyboard = false;
            _registeredHid = false;
            return;
        }

        var count = (_registeredKeyboard ? 1 : 0) + (_registeredHid ? 1 : 0);
        if (count > 0)
        {
            var devices = new RAWINPUTDEVICE[count];
            var index = 0;
            if (_registeredKeyboard)
                devices[index++] = new() { usUsagePage = GenericDesktopPage, usUsage = KeyboardUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE };
            if (_registeredHid)
                devices[index] = new() { usUsagePage = AcerGamingPage, usUsage = AcerGamingUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE };
            PInvoke.RegisterRawInputDevices(devices, (uint)sizeof(RAWINPUTDEVICE));
        }

        _registeredKeyboard = false;
        _registeredHid = false;
        _window.Dispose();
        _window = null;
    }

    private void ScheduleRetry()
    {
        if (_tracker.ScheduleRetry(EnsureStarted) is { } delay)
            LogRetryScheduled(delay);
    }

    private void HookSystemEvents()
    {
        if (_systemEventsHooked)
            return;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _systemEventsHooked = true;
    }

    private void UnhookSystemEvents()
    {
        if (!_systemEventsHooked)
            return;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _systemEventsHooked = false;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is not (SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon))
            return;
        var reRegister = e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon;
        _dispatcher.TryEnqueue(() => RecoverAfterDesktopTransition(reRegister));
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Resume)
            _dispatcher.TryEnqueue(() => RecoverAfterDesktopTransition(reRegister: true));
    }

    private void RecoverAfterDesktopTransition(bool reRegister)
    {
        if (_disposed || !_tracker.IsEnabled)
            return;
        _tracker.ResetKeyState();
        if (!reRegister)
            return;
        // Re-apply Raw Input registration on unlock/logon/resume in case early startup hit a desktop transition.
        _tracker.ResetRetries();
        _registeredKeyboard = false;
        _registeredHid = false;
        EnsureStarted();
    }

    private void OnMessage(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == PInvoke.WM_INPUT)
            OnInput(new HRAWINPUT(lParam.Value));
    }

    private void OnInput(HRAWINPUT input)
    {
        uint size = 0;
        var headerSize = (uint)sizeof(RAWINPUTHEADER);
        if (PInvoke.GetRawInputData(input, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, default, ref size, headerSize) != 0
            || size < headerSize
            || size > MaxRawInputSize)
            return;
        var buffer = stackalloc byte[(int)size];
        if (PInvoke.GetRawInputData(input, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, new Span<byte>(buffer, (int)size), ref size, headerSize) != size)
            return;
        var raw = (RAWINPUT*)buffer;

        if (raw->header.dwType == (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEKEYBOARD)
        {
            var key = raw->data.keyboard;
            if (_tracker.HandleKeyboardInput(key.MakeCode, (key.Flags & PInvoke.RI_KEY_E0) != 0, (key.Flags & PInvoke.RI_KEY_BREAK) != 0))
                Press();
        }
        else if (raw->header.dwType == (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEHID)
        {
            // Keep hid as a pointer into buffer: copying raw->data.hid truncates bRawData to its 1-byte placeholder.
            RAWHID* hid = &raw->data.hid;
            var payloadOffset = (uint)((byte*)&hid->bRawData - buffer);
            if (size <= payloadOffset)
                return;
            var payload = new ReadOnlySpan<byte>(&hid->bRawData, (int)(size - payloadOffset));
            var reportSize = (int)Math.Min(hid->dwSizeHid, (uint)int.MaxValue);
            var reportCount = (int)Math.Min(hid->dwCount, (uint)int.MaxValue);
            if (NitroSenseKeyTracker.MatchesHidReports(payload, reportSize, reportCount))
                Press();
        }
    }

    private void Press()
    {
        LogPressed();
        Pressed?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
        _tracker.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening for the NitroSense key")]
    private partial void LogListening();

    [LoggerMessage(Level = LogLevel.Information, Message = "NitroSense key pressed")]
    private partial void LogPressed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not listen for the NitroSense key: {Call} failed ({Error})")]
    private partial void LogFailed(string call, int error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retrying NitroSense key registration in {Delay}")]
    private partial void LogRetryScheduled(TimeSpan delay);
}
