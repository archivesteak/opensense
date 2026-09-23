using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OpenSense.App.Services;
using OpenSense.App.Startup;
using OpenSense.App.ViewModels;
using OpenSense.Core.Settings;
using OpenSense.Core.Updates;
using Serilog;

namespace OpenSense.App;

public partial class App : Application
{
    private readonly LaunchOptions _options;
    private readonly SingleInstance _instance;
    private IHost? _host;
    private MainWindow? _window;
    private bool _exiting;

    public App(LaunchOptions options, SingleInstance instance)
    {
        _options = options;
        _instance = instance;
        // OpenSense is English-only for now; keep WinUI's built-in strings (Settings, On/Off) in the same language.
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
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

        _window = Services.GetRequiredService<MainWindow>();
        Services.GetRequiredService<TrayService>().Initialize();
        _instance.Activated += () => _window.DispatcherQueue.TryEnqueue(ShowMainWindow);
        _instance.ExitRequested += () => _window.DispatcherQueue.TryEnqueue(Quit);

        var settings = Services.GetRequiredService<SettingsService>().Current;
        if (!_options.Autostart && !settings.Ui.StartMinimized)
            _window.Activate();

        Services.GetRequiredService<UpdateViewModel>().Start();
        var nitroSenseKey = Services.GetRequiredService<NitroSenseKey>();
        nitroSenseKey.Pressed += ShowMainWindow;
        if (settings.Ui.OpenWithNitroSenseKey)
            nitroSenseKey.Start();
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
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<NitroSenseKey>();
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
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<UpdateViewModel>();

        builder.Services.AddSingleton<MainWindow>();
        return builder.Build();
    }

    public void ShowMainWindow()
    {
        if (_window is null)
            return;
        _window.AppWindow.Show();
        _window.Activate();
    }

    /// <summary>Retry elevation from the "administrator rights needed" screen (portable copy).</summary>
    public void RelaunchElevated()
    {
        _instance.Dispose(); // let the elevated copy become the running instance
        if (Program.TryRelaunchElevated(_options))
            Quit();
    }

    public void Quit()
    {
        if (_exiting)
            return;
        _exiting = true;
        if (_host is not null)
        {
            Services.GetRequiredService<NitroSenseKey>().Dispose();
            Services.GetRequiredService<TrayService>().Dispose();
            Services.GetRequiredService<DeviceSession>().Dispose(); // the service keeps control; a hosted engine hands the fans back
            Services.GetRequiredService<SettingsService>().Dispose();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        Exit();
    }

    public bool IsExiting => _exiting;

    /// <summary>The main window is on screen (not hidden to the notification area).</summary>
    public bool IsWindowVisible => _window?.AppWindow.IsVisible == true;

    /// <summary>This build's version, and the GitHub repository (from RepositoryUrl in Directory.Build.props) to update from.</summary>
    private static UpdaterOptions UpdaterOptions()
    {
        var assembly = typeof(App).Assembly;
        var version = assembly.GetName().Version ?? new Version(0, 0, 0);
        var repository = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "RepositoryUrl").Value!;
        return new UpdaterOptions(new Uri(repository).AbsolutePath.Trim('/'), new Version(version.Major, version.Minor, version.Build));
    }
}
