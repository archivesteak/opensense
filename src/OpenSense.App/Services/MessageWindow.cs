using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OpenSense.App.Services;

/// <summary>
/// A hidden message-only window (parent HWND_MESSAGE) for Win32 input that arrives as window messages: raw input
/// for the NitroSense key, WM_HOTKEY for the shortcut. Create it on the UI thread, whose message loop delivers them.
/// </summary>
internal sealed unsafe class MessageWindow : IDisposable
{
    private readonly string _className;
    private readonly Action<uint, WPARAM, LPARAM> _onMessage;
    private readonly WNDPROC _windowProc; // kept alive while the window exists

    private MessageWindow(string className, Action<uint, WPARAM, LPARAM> onMessage)
    {
        _className = className;
        _onMessage = onMessage;
        _windowProc = WindowProc;
    }

    public HWND Handle { get; private set; }

    /// <summary>The window, or null when Windows refused; <paramref name="failure"/> then names the call and its error.</summary>
    public static MessageWindow? Create(string className, Action<uint, WPARAM, LPARAM> onMessage, out (string Call, int Error) failure)
    {
        failure = default;
        var window = new MessageWindow(className, onMessage);
        fixed (char* name = className)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = window._windowProc,
                hInstance = Instance,
                lpszClassName = name,
            };
            if (PInvoke.RegisterClassEx(windowClass) == 0)
            {
                failure = ("RegisterClassEx", Marshal.GetLastPInvokeError());
                return null;
            }
        }

        window.Handle = PInvoke.CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, new HWND(-3), null, null, null);
        if (window.Handle == HWND.Null)
        {
            failure = ("CreateWindowEx", Marshal.GetLastPInvokeError());
            window.UnregisterClass();
            return null;
        }
        return window;
    }

    private static HINSTANCE Instance => (HINSTANCE)(nint)PInvoke.GetModuleHandle((PCWSTR)null);

    private LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        _onMessage(message, wParam, lParam);
        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void UnregisterClass()
    {
        fixed (char* name = _className)
            PInvoke.UnregisterClass(name, Instance);
    }

    public void Dispose()
    {
        if (Handle == HWND.Null)
            return;
        PInvoke.DestroyWindow(Handle);
        Handle = HWND.Null;
        UnregisterClass();
    }
}
