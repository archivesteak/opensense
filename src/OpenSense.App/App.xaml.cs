using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using OpenSense.App.Services;
using OpenSense.App.Startup;
using OpenSense.App.ViewModels;
using OpenSense.Core.Settings;
using OpenSense.Core.Updates;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OpenSense.App;

public partial class App : Application
{
    private readonly LaunchOptions _options;
    private readonly SingleInstance _instance;
    private IHost? _host;
    private MainWindow? _window;
    private bool _exiting;
    private bool _showPending;
    private bool _sessionEndedHooked;

    public App(LaunchOptions options, SingleInstance instance)
    {
        _options = options;
        _instance = instance;
        InitializeComponent();
        UnhandledException += (_, e) => Log.Error(e.Exception, "Unhandled UI exception");
    }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("The app has not started yet.");

    public LaunchOptions Options => _options;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host = BuildHost();
        await _host.StartAsync();
        HookSessionEnded();
        var autostartRefresh = Task.Run(AutostartService.Refresh); // a portable copy's sign-in task follows it when it was moved

        // Start key/shortcut listeners before slow window, tray, and notification COM setup at Windows sign-in.
        var settings = Services.GetRequiredService<SettingsService>().Current;
        var dispatcher = Services.GetRequiredService<DispatcherQueue>();
        _instance.Activated += () => dispatcher.TryEnqueue(RequestShowMainWindow);
        _instance.ExitRequested += () => dispatcher.TryEnqueue(Quit);

        var nitroSenseKey = Services.GetRequiredService<NitroSenseKey>();
        nitroSenseKey.Pressed += RequestShowMainWindow;
        if (settings.Ui.OpenWithNitroSenseKey)
            nitroSenseKey.Start();
        var shortcut = Services.GetRequiredService<OpenShortcut>();
        shortcut.Pressed += RequestShowMainWindow;
        shortcut.Set(settings.Ui.OpenShortcut);

        _window = Services.GetRequiredService<MainWindow>();
        _window.AppWindow.Changed += (_, e) =>
        {
            if (e.DidVisibilityChange)
                WindowVisibilityChanged?.Invoke();
        };
        if (_showPending)
        {
            _showPending = false;
            ShowMainWindow();
        }
        else if (!_options.Autostart)
            _window.Activate();

        Services.GetRequiredService<TrayService>().Initialize();
        Services.GetRequiredService<ToastService>().Initialize();
        Services.GetRequiredService<UpdateViewModel>().Start();
        await autostartRefresh;
        await Services.GetRequiredService<ShellViewModel>().InitializeAsync();
    }

    private IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSense", "logs");
        builder.Services.AddSerilog(log => log
            .MinimumLevel.Warning()
            .WriteTo.File(Path.Combine(logDirectory, "opensense-.log"), formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));

        builder.Services.AddSingleton(_options);
        builder.Services.AddSingleton(DispatcherQueue.GetForCurrentThread());
        builder.Services.AddSingleton(new SettingsStore<UserSettings>(SettingsPaths.User));
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<DeviceSession>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<TrayService>();
        builder.Services.AddSingleton<ToastService>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<NitroSenseKey>();
        builder.Services.AddSingleton<OpenShortcut>();
        builder.Services.AddSingleton(UpdaterOptions());
        builder.Services.AddHttpClient<GitHubUpdater>(client =>
        {
            // GitHub's REST API requires a User-Agent; pin the API version it answers with.
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenSense", UpdaterOptions().CurrentVersion.ToString(3)));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.Timeout = TimeSpan.FromMinutes(10); // the installer is ~80 MB
        });

        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<MonitorViewModel>();
        builder.Services.AddSingleton<FanControlViewModel>();
        builder.Services.AddSingleton<LightingViewModel>();
        builder.Services.AddSingleton<SystemViewModel>();
        builder.Services.AddSingleton<BatteryViewModel>();
        builder.Services.AddSingleton<StartupViewModel>();
        builder.Services.AddSingleton<GpuClocksViewModel>();
        builder.Services.AddSingleton<BiosSettingsViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<UpdateViewModel>();

        builder.Services.AddSingleton<MainWindow>();
        return builder.Build();
    }

    private void RequestShowMainWindow()
    {
        if (_exiting)
            return;
        if (_window is null)
        {
            _showPending = true;
            return;
        }
        ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_window is null || _exiting)
            return;

        if (_window.AppWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
            presenter.Restore();

        _window.AppWindow.Show();
        _window.Activate();
        BringToForeground(new HWND(WinRT.Interop.WindowNative.GetWindowHandle(_window)));
    }

    private static unsafe void BringToForeground(HWND hwnd)
    {
        var foreground = PInvoke.GetForegroundWindow();
        if (foreground == hwnd)
            return;

        var currentThread = PInvoke.GetCurrentThreadId();
        var foregroundThread = foreground == HWND.Null ? 0 : PInvoke.GetWindowThreadProcessId(foreground, null);
        var attached = foregroundThread != 0 && foregroundThread != currentThread
            && PInvoke.AttachThreadInput(foregroundThread, currentThread, true);
        try
        {
            PInvoke.BringWindowToTop(hwnd);
            if (!PInvoke.SetForegroundWindow(hwnd))
            {
                // Background raw input (RIDEV_INPUTSINK) has no foreground lock grant when another window owns
                // focus; briefly toggling TOPMOST lifts the window without leaving it pinned on top.
                const SET_WINDOW_POS_FLAGS flags = SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
                PInvoke.SetWindowPos(hwnd, new HWND(-1), 0, 0, 0, 0, flags);
                PInvoke.SetWindowPos(hwnd, new HWND(-2), 0, 0, 0, 0, flags);
                PInvoke.SetForegroundWindow(hwnd);
            }
        }
        finally
        {
            if (attached)
                PInvoke.AttachThreadInput(foregroundThread, currentThread, false);
        }
    }

    private void HookSessionEnded()
    {
        if (_sessionEndedHooked)
            return;
        SystemEvents.SessionEnded += OnSessionEnded;
        _sessionEndedHooked = true;
    }

    private void UnhookSessionEnded()
    {
        if (!_sessionEndedHooked)
            return;
        SystemEvents.SessionEnded -= OnSessionEnded;
        _sessionEndedHooked = false;
    }

    private void OnSessionEnded(object? sender, SessionEndedEventArgs e)
    {
        if (_exiting || _host is not { } host)
            return;
        try
        {
            host.Services.GetRequiredService<SettingsService>().Flush();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Retry elevation from the "administrator rights needed" screen (portable copy).</summary>
    public void RelaunchElevated()
    {
        _instance.Dispose(); // let the elevated copy become the running instance
        if (Program.TryRelaunchElevated(_options))
            Quit();
    }

    /// <summary>Quits and opens a new copy on the Settings page, e.g. to show a new language.</summary>
    public void Restart()
    {
        _instance.Dispose(); // let the new copy become the running instance once this one has exited
        var arguments = new LaunchOptions { Page = "settings", WaitForProcess = Environment.ProcessId }.ToArguments();
        // Started directly, not through the shell, so it keeps this copy's rights (a portable copy runs elevated).
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, arguments) { UseShellExecute = false })?.Dispose();
        Quit();
    }

    public void Quit()
    {
        if (_exiting)
            return;
        _exiting = true;
        UnhookSessionEnded();
        if (_host is { } host)
        {
            // Each part closes whatever the one before did: a hosted engine must still hand the fans back.
            Close(Services.GetRequiredService<NitroSenseKey>);
            Close(Services.GetRequiredService<OpenShortcut>);
            Close(Services.GetRequiredService<ToastService>);
            Close(Services.GetRequiredService<TrayService>);
            Close(Services.GetRequiredService<DeviceSession>); // the service keeps control; a hosted engine hands the fans back
            Close(Services.GetRequiredService<SettingsService>);
            Close(() => host);
        }
        Log.CloseAndFlush();
        Exit();
    }

    private static void Close<T>(Func<T> part) where T : IDisposable
    {
        try
        {
            part().Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Error(ex, "Closing {Part} failed", typeof(T).Name);
        }
    }

    public bool IsExiting => _exiting;

    /// <summary>The main window is on screen (not hidden to the notification area).</summary>
    public bool IsWindowVisible => _window?.AppWindow.IsVisible == true;

    /// <summary>The main window was shown or hidden (<see cref="IsWindowVisible"/>).</summary>
    public event Action? WindowVisibilityChanged;

    /// <summary>This build's version, and the GitHub repository (from RepositoryUrl in Directory.Build.props) to update from.</summary>
    private static UpdaterOptions UpdaterOptions()
    {
        var assembly = typeof(App).Assembly;
        var version = assembly.GetName().Version ?? new Version(0, 0, 0);
        var repository = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "RepositoryUrl").Value!;
        return new UpdaterOptions(new Uri(repository).AbsolutePath.Trim('/'), new Version(version.Major, version.Minor, version.Build));
    }
}
