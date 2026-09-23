using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OpenSense.App.Services;
using OpenSense.App.Startup;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;

namespace OpenSense.App;

public static class Program
{
    private const int ErrorCancelled = 1223; // the user declined the UAC prompt
    private static readonly TimeSpan ServiceStartTimeout = TimeSpan.FromSeconds(30);

    [STAThread]
    private static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        switch (options.Command)
        {
            case MaintenanceCommand.Exit:
                return SingleInstance.RequestExit(TimeSpan.FromSeconds(15)) ? 0 : 1;
            case MaintenanceCommand.EnableAutostart:
                AutostartService.Enable();
                return 0;
            case MaintenanceCommand.DisableAutostart:
                AutostartService.Disable();
                return 0;
            case MaintenanceCommand.StartService:
                return StartService() ? 0 : 1;
            case MaintenanceCommand.ApplyUpdate:
                return ApplyUpdate(options);
        }

        // Already running? Bring it forward.
        if (SingleInstance.SignalExisting())
            return 0;

        // Installed copies talk to the OpenSense service and need no rights of their own. A portable copy
        // (no service) owns the firmware itself, which needs administrator rights.
        if (!OpenSensePipe.IsServiceInstalled && !SystemInfo.IsElevated && TryRelaunchElevated(options))
            return 0;

        using var instance = SingleInstance.Claim();
        if (instance is null)
            return 0;

        UseEnglishUi();
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App(options, instance);
        });
        return 0;
    }

    /// <summary>
    /// OpenSense ships in English only for now. WinUI loads its built-in strings (toggle labels,
    /// dialog buttons, the colour picker) from .mui resources by the process's preferred UI
    /// languages, so set those to match rather than mixing languages on screen.
    /// </summary>
    private static unsafe void UseEnglishUi()
    {
        fixed (char* languages = "en-US\0\0")
        {
            Windows.Win32.PInvoke.SetProcessPreferredUILanguages(Windows.Win32.PInvoke.MUI_LANGUAGE_NAME, languages, null);
        }
    }

    public static bool TryRelaunchElevated(LaunchOptions options) => TryRunElevated(options.ToArguments(), wait: false);

    /// <summary>Runs this executable elevated with <paramref name="arguments"/>; false if the user declined.</summary>
    public static bool TryRunElevated(string arguments, bool wait)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (wait)
            {
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    private static int ApplyUpdate(LaunchOptions options)
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSense", "logs", "update.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        void Log(string message) => File.AppendAllText(logPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        try
        {
            UpdateInstaller.ApplyPortableUpdate(options.UpdateTarget!, options.WaitForProcess, Log);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.TimeoutException)
        {
            Log(ex.ToString());
            Windows.Win32.PInvoke.MessageBox(default,
                $"OpenSense could not be updated, so the previous version was kept.\n\n{ex.Message}\n\nDetails: {logPath}",
                "OpenSense update", Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_ICONWARNING);
            return 1;
        }
    }

    private static bool StartService()
    {
        using var service = new ServiceController(OpenSensePipe.ServiceName);
        try
        {
            if (service.Status != ServiceControllerStatus.Running)
            {
                if (service.Status is ServiceControllerStatus.Stopped)
                    service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, ServiceStartTimeout);
            }
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ServiceProcess.TimeoutException or Win32Exception)
        {
            return false;
        }
    }
}
