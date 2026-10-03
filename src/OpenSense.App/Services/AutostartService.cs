using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;
using Serilog;

namespace OpenSense.App.Services;

/// <summary>
/// Starts the OpenSense window in the notification area when the user signs in. An installed copy uses the per-user Run
/// key; fan control itself does not depend on it, the service runs from boot. A portable copy (no service) runs elevated,
/// which a Run entry would make Windows ask about at every sign-in, so it uses a scheduled task that starts it with full
/// rights instead (<see cref="AutostartTask"/>).
/// </summary>
public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OpenSense";

    private static string Command => $"\"{Environment.ProcessPath}\" --autostart";

    private static string ThisExe => Path.GetFullPath(Environment.ProcessPath!);

    /// <summary>A portable copy: no service is registered.</summary>
    private static bool UsesTask => !OpenSensePipe.IsServiceInstalled;

    // Task names are machine-wide, so each user gets one of their own.
    private static string TaskName => $"OpenSense autostart ({Environment.UserName})";

    /// <summary>Something starts this copy when the user signs in.</summary>
    public static bool IsEnabled => RunEntryIsThisCopy || (UsesTask && TaskIsThisCopy);

    /// <exception cref="UnauthorizedAccessException">A portable copy needs administrator rights to make the task.</exception>
    public static void Enable()
    {
        if (!UsesTask)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(ValueName, Command);
            return;
        }
        // The task first: if that fails, nothing has changed.
        ScheduledTasks.Register(TaskName, AutostartTask.BuildXml(ThisExe, CurrentUserSid()));
        RemoveRunEntry();
    }

    public static void Disable()
    {
        if (UsesTask)
            ScheduledTasks.Delete(TaskName);
        RemoveRunEntry();
    }

    /// <summary>
    /// For a portable copy, at start: keeps the task pointing at this copy, so a copy that was moved takes its task with it
    /// and an old Run entry (from versions that had no task) moves into the task. Needs the administrator rights a portable
    /// copy has while it runs; a failure is only logged.
    /// </summary>
    public static void Refresh()
    {
        if (!UsesTask || !SystemInfo.IsElevated)
            return;
        try
        {
            if (AutostartTask.NeedsWrite(TaskTarget(), RunEntryIsThisCopy, ThisExe, File.Exists))
                Enable();
        }
        catch (Exception ex) when (IsTaskFailure(ex))
        {
            Log.Warning(ex, "Could not update the sign-in task");
        }
    }

    private static bool RunEntryIsThisCopy
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TaskIsThisCopy
    {
        get
        {
            try
            {
                return TaskTarget() is { } target && AutostartTask.SamePath(target, ThisExe);
            }
            catch (Exception ex) when (IsTaskFailure(ex))
            {
                return false;
            }
        }
    }

    /// <summary>What the user's sign-in task starts; null when there is none (or it is switched off).</summary>
    private static string? TaskTarget() => ScheduledTasks.ReadXml(TaskName) is { } xml ? AutostartTask.ReadTarget(xml) : null;

    private static void RemoveRunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string CurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User!.Value;
    }

    /// <summary>Task Scheduler was unreachable or refused (no administrator rights, service stopped).</summary>
    private static bool IsTaskFailure(Exception ex) => ex is COMException or UnauthorizedAccessException or IOException;
}
