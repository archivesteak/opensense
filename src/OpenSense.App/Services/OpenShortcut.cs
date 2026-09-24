using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace OpenSense.App.Services;

/// <summary>
/// The key or key combination chosen in Settings to open OpenSense from anywhere. It is a system-wide hot key
/// (RegisterHotKey), so the app in front does not also react to it. Raises <see cref="Pressed"/> on the UI thread.
/// </summary>
public sealed partial class OpenShortcut(ILogger<OpenShortcut> log) : IDisposable
{
    private const int HotKeyId = 1;
    private const string WindowClass = "OpenSense.OpenShortcut";

    private MessageWindow? _window;

    public event Action? Pressed;

    /// <summary>A shortcut is registered and working.</summary>
    public bool IsSet => _window is not null;

    /// <summary>
    /// Makes <paramref name="shortcut"/> the one that opens OpenSense, in place of the current one (null: none).
    /// False when Windows refuses it, usually because another app already uses it; nothing is registered then.
    /// </summary>
    public bool Set(KeyShortcut? shortcut)
    {
        Clear();
        if (shortcut is null)
            return true;

        _window = MessageWindow.Create(WindowClass, OnMessage, out var failure);
        if (_window is null)
        {
            LogFailed(failure.Call, failure.Error);
            return false;
        }
        if (!PInvoke.RegisterHotKey(_window.Handle, HotKeyId, Modifiers(shortcut), (uint)shortcut.Key))
        {
            LogFailed("RegisterHotKey", Marshal.GetLastPInvokeError());
            Clear();
            return false;
        }
        return true;
    }

    private void Clear()
    {
        if (_window is null)
            return;
        PInvoke.UnregisterHotKey(_window.Handle, HotKeyId);
        _window.Dispose();
        _window = null;
    }

    private static HOT_KEY_MODIFIERS Modifiers(KeyShortcut shortcut) =>
        HOT_KEY_MODIFIERS.MOD_NOREPEAT
        | (shortcut.Control ? HOT_KEY_MODIFIERS.MOD_CONTROL : 0)
        | (shortcut.Alt ? HOT_KEY_MODIFIERS.MOD_ALT : 0)
        | (shortcut.Shift ? HOT_KEY_MODIFIERS.MOD_SHIFT : 0)
        | (shortcut.Windows ? HOT_KEY_MODIFIERS.MOD_WIN : 0);

    private void OnMessage(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == PInvoke.WM_HOTKEY && wParam.Value == HotKeyId)
        {
            LogPressed();
            Pressed?.Invoke();
        }
    }

    public void Dispose() => Clear();

    [LoggerMessage(Level = LogLevel.Information, Message = "Shortcut pressed")]
    private partial void LogPressed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not register the shortcut: {Call} failed ({Error})")]
    private partial void LogFailed(string call, int error);
}
