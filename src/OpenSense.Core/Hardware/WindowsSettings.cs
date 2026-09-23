using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OpenSense.Core.Hardware;

/// <summary>Windows Sticky Keys, as NitroSense's "Sticky key" switch manages it (feature + Shift×5 shortcut).</summary>
public static class StickyKeys
{
    private const STICKYKEYS_FLAGS Managed = STICKYKEYS_FLAGS.SKF_STICKYKEYSON | STICKYKEYS_FLAGS.SKF_HOTKEYACTIVE;

    public static unsafe bool? IsEnabled()
    {
        var keys = new STICKYKEYS { cbSize = (uint)sizeof(STICKYKEYS) };
        if (!PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSTICKYKEYS, keys.cbSize, &keys, 0))
            return null;
        return (keys.dwFlags & Managed) == Managed;
    }

    public static unsafe bool SetEnabled(bool enabled)
    {
        var keys = new STICKYKEYS { cbSize = (uint)sizeof(STICKYKEYS) };
        if (!PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSTICKYKEYS, keys.cbSize, &keys, 0))
            return false;
        keys.dwFlags = enabled ? keys.dwFlags | Managed : keys.dwFlags & ~Managed;
        return PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETSTICKYKEYS, keys.cbSize, &keys,
            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE | SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDCHANGE);
    }
}

public sealed record PowerPlan(Guid Id, string Name);

/// <summary>Classic Windows power plans (what NitroSense switches on models without operating modes).</summary>
public static class PowerPlans
{
    public static unsafe IReadOnlyList<PowerPlan> List()
    {
        var plans = new List<PowerPlan>();
        for (uint index = 0; ; index++)
        {
            Guid id;
            var size = (uint)sizeof(Guid);
            if (PInvoke.PowerEnumerate(default, null, null, POWER_DATA_ACCESSOR.ACCESS_SCHEME, index, (byte*)&id, &size) != WIN32_ERROR.ERROR_SUCCESS)
                break;
            plans.Add(new PowerPlan(id, ReadName(id)));
        }
        return plans;
    }

    public static unsafe Guid? Active()
    {
        Guid* active;
        if (PInvoke.PowerGetActiveScheme(default, &active) != WIN32_ERROR.ERROR_SUCCESS)
            return null;
        try
        {
            return *active;
        }
        finally
        {
            PInvoke.LocalFree((HLOCAL)active);
        }
    }

    public static unsafe bool SetActive(Guid id) =>
        PInvoke.PowerSetActiveScheme(default, &id) == WIN32_ERROR.ERROR_SUCCESS;

    private static unsafe string ReadName(Guid id)
    {
        uint size = 0;
        if (PInvoke.PowerReadFriendlyName(default, &id, null, null, null, &size) != WIN32_ERROR.ERROR_SUCCESS || size == 0)
            return id.ToString();
        var buffer = new byte[size];
        fixed (byte* raw = buffer)
        {
            if (PInvoke.PowerReadFriendlyName(default, &id, null, null, raw, &size) != WIN32_ERROR.ERROR_SUCCESS)
                return id.ToString();
        }
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }
}

/// <summary>Restarts Windows (after a GPU mode switch).</summary>
public static class WindowsRestart
{
    public static unsafe bool RestartNow()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        if (!PInvoke.OpenProcessToken(process.SafeHandle,
                Windows.Win32.Security.TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | Windows.Win32.Security.TOKEN_ACCESS_MASK.TOKEN_QUERY,
                out var token))
            return false;

        using (token)
        {
            if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
                return false;
            var privileges = new Windows.Win32.Security.TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            privileges.Privileges[0] = new Windows.Win32.Security.LUID_AND_ATTRIBUTES
            {
                Luid = luid,
                Attributes = Windows.Win32.Security.TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED,
            };
            if (!PInvoke.AdjustTokenPrivileges(token, false, &privileges, []))
                return false;
        }

        return PInvoke.InitiateShutdown(null, null, 0,
            Windows.Win32.System.Shutdown.SHUTDOWN_FLAGS.SHUTDOWN_RESTART,
            Windows.Win32.System.Shutdown.SHUTDOWN_REASON.SHTDN_REASON_MAJOR_APPLICATION
            | Windows.Win32.System.Shutdown.SHUTDOWN_REASON.SHTDN_REASON_MINOR_RECONFIG
            | Windows.Win32.System.Shutdown.SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED) == 0;
    }
}
