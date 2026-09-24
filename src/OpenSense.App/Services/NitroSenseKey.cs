using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
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
public sealed unsafe partial class NitroSenseKey(ILogger<NitroSenseKey> log) : IDisposable
{
    private const ushort ScanCode = 0x75;
    private const ushort GenericDesktopPage = 0x01, KeyboardUsage = 0x06;
    private const ushort AcerGamingPage = 0x88, AcerGamingUsage = 0x01;
    private const string WindowClass = "OpenSense.NitroSenseKey";

    private MessageWindow? _window;
    private bool _keyDown;

    public event Action? Pressed;

    public bool IsListening => _window is not null;

    /// <summary>Starts listening; call on the UI thread (its message loop delivers the input).</summary>
    public void Start()
    {
        if (IsListening)
            return;

        _window = MessageWindow.Create(WindowClass, OnMessage, out var failure);
        if (_window is null)
        {
            LogFailed(failure.Call, failure.Error);
            return;
        }

        RAWINPUTDEVICE[] devices =
        [
            new() { usUsagePage = GenericDesktopPage, usUsage = KeyboardUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK, hwndTarget = _window.Handle },
            new() { usUsagePage = AcerGamingPage, usUsage = AcerGamingUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK, hwndTarget = _window.Handle },
        ];
        if (!PInvoke.RegisterRawInputDevices(devices, (uint)sizeof(RAWINPUTDEVICE)))
        {
            LogFailed("RegisterRawInputDevices", Marshal.GetLastPInvokeError());
            Stop();
            return;
        }
        LogListening();
    }

    public void Stop()
    {
        if (_window is null)
            return;
        RAWINPUTDEVICE[] devices =
        [
            new() { usUsagePage = GenericDesktopPage, usUsage = KeyboardUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE },
            new() { usUsagePage = AcerGamingPage, usUsage = AcerGamingUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE },
        ];
        PInvoke.RegisterRawInputDevices(devices, (uint)sizeof(RAWINPUTDEVICE));
        _window.Dispose();
        _window = null;
        _keyDown = false;
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
        if (PInvoke.GetRawInputData(input, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, default, ref size, headerSize) != 0 || size == 0)
            return;
        var buffer = stackalloc byte[(int)size];
        if (PInvoke.GetRawInputData(input, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, new Span<byte>(buffer, (int)size), ref size, headerSize) != size)
            return;
        var raw = (RAWINPUT*)buffer;

        if (raw->header.dwType == (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEKEYBOARD)
        {
            var key = raw->data.keyboard;
            if (key.MakeCode != ScanCode || (key.Flags & PInvoke.RI_KEY_E0) == 0)
                return;
            var down = (key.Flags & PInvoke.RI_KEY_BREAK) == 0;
            // Holding the key repeats key-down; one press opens OpenSense once.
            if (down && !_keyDown)
                Press();
            _keyDown = down;
        }
        else if (raw->header.dwType == (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEHID)
        {
            var hid = raw->data.hid;
            var report = new ReadOnlySpan<byte>(&hid.bRawData, (int)Math.Min(hid.dwSizeHid, size - headerSize - 8));
            if (report.Length >= 3 && report[1] == 0x81 && report[2] == 0xFF)
                Press();
        }
    }

    private void Press()
    {
        LogPressed();
        Pressed?.Invoke();
    }

    public void Dispose() => Stop();

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening for the NitroSense key")]
    private partial void LogListening();

    [LoggerMessage(Level = LogLevel.Information, Message = "NitroSense key pressed")]
    private partial void LogPressed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not listen for the NitroSense key: {Call} failed ({Error})")]
    private partial void LogFailed(string call, int error);
}
