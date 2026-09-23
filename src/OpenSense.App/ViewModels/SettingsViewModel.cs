using System.Diagnostics;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;
using Windows.ApplicationModel.DataTransfer;

namespace OpenSense.App.ViewModels;

/// <summary>App preferences, capability overrides, diagnostics and about.</summary>
public sealed partial class SettingsViewModel(SettingsService settings, DeviceSession session, NotificationService notifications, NitroSenseKey nitroSenseKey)
    : ObservableObject
{
    private static readonly int[] Intervals = [500, 1000, 2000];
    private static readonly string AppLogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSense", "logs");
    private static readonly string ServiceLogDirectory = Path.Combine(SettingsPaths.MachineDirectory, "logs");

    private bool _loading;

    /// <summary>In <see cref="Intervals"/> order.</summary>
    public static IReadOnlyList<string> IntervalNames { get; } =
        [.. Intervals.Select(ms => Strings.Format("Unit_Seconds", ms / 1000.0))];

    public static IReadOnlyList<string> ThemeNames { get; } = [Strings.Get("Theme_System"), Strings.Get("Theme_Light"), Strings.Get("Theme_Dark")];

    /// <summary>"Use system setting" (an empty tag), then every translation by its own name.</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } = [new("", Strings.Get("Language_System")), .. AppLanguage.Available];

    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool StartMinimized { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial bool UseFahrenheit { get; set; }

    [ObservableProperty]
    public partial bool OpenWithNitroSenseKey { get; set; }

    [ObservableProperty]
    public partial int IntervalIndex { get; set; } = 1;

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    /// <summary>The chosen language isn't the one on screen until OpenSense restarts.</summary>
    [ObservableProperty]
    public partial bool LanguageRestartNeeded { get; set; }

    /// <summary>The firmware lists operating modes, so the user can switch them on or off.</summary>
    [ObservableProperty]
    public partial bool OperatingModesSwitchAvailable { get; set; }

    [ObservableProperty]
    public partial bool OperatingModesOn { get; set; }

    [ObservableProperty]
    public partial string OperatingModesDescription { get; set; } = "";

    [ObservableProperty]
    public partial string Diagnostics { get; set; } = "";

    [ObservableProperty]
    public partial string LaptopModel { get; set; } = "";

    [ObservableProperty]
    public partial string BiosVersion { get; set; } = "";

    [ObservableProperty]
    public partial string TemperatureSources { get; set; } = "";

    [ObservableProperty]
    public partial bool PawnIOMissing { get; set; }

    /// <summary>How this copy reaches the laptop, for the About section.</summary>
    [ObservableProperty]
    public partial string ModeDescription { get; set; } = "";

    [ObservableProperty]
    public partial bool HasServiceLogs { get; set; }

    /// <summary>Raised when the theme choice changes (the window applies it).</summary>
    public event Action<ElementTheme>? ThemeChanged;

    public ElementTheme Theme => ThemeIndex switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        var ui = settings.Current.Ui;
        var machine = session.Settings;
        StartWithWindows = AutostartService.IsEnabled;
        StartMinimized = ui.StartMinimized;
        CloseToTray = ui.CloseToTray;
        UseFahrenheit = ui.UseFahrenheit;
        OpenWithNitroSenseKey = ui.OpenWithNitroSenseKey;
        IntervalIndex = Math.Max(0, Array.IndexOf(Intervals, machine.PollIntervalMs));
        ThemeIndex = ui.Theme;
        LanguageIndex = Math.Max(0, Languages.ToList().FindIndex(l => l.Tag.Length > 0 && string.Equals(l.Tag, ui.Language, StringComparison.OrdinalIgnoreCase)));

        var detected = session.Detected;
        OperatingModesSwitchAvailable = detected.FirmwareOperatingModes.Count > 0 || detected.HasOperatingModes;
        OperatingModesOn = session.Capabilities.HasOperatingModes;
        OperatingModesDescription = DescribeOperatingModes(detected);

        Diagnostics = detected.Diagnostics;
        LaptopModel = session.DeviceName ?? Strings.Get("Settings_UnknownModel");
        BiosVersion = session.BiosVersion is { } bios ? Strings.Format("Settings_Bios", bios) : Strings.Get("Settings_BiosUnknown");
        var sources = session.TemperatureSources;
        TemperatureSources = Strings.Format("Settings_SensorSources", Names.TemperatureSource(sources.Cpu), Names.TemperatureSource(sources.Gpu));
        PawnIOMissing = sources.PawnIOMissing;
        ModeDescription = Strings.Get(session.IsPortable ? "Settings_Mode_Portable" : "Settings_Mode_Installed");
        HasServiceLogs = session.UsesService;
        _loading = false;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
            return;
        try
        {
            if (value)
                AutostartService.Enable();
            else
                AutostartService.Disable();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            notifications.Show(Strings.Get("Notice_Autostart_Title"), Strings.Format("Notice_AutostartFailed", ex.Message), InfoBarSeverity.Error);
        }
    }

    partial void OnStartMinimizedChanged(bool value) => UpdateUi(u => u with { StartMinimized = value });

    partial void OnCloseToTrayChanged(bool value) => UpdateUi(u => u with { CloseToTray = value });

    partial void OnUseFahrenheitChanged(bool value) => UpdateUi(u => u with { UseFahrenheit = value });

    partial void OnOpenWithNitroSenseKeyChanged(bool value)
    {
        if (_loading)
            return;
        UpdateUi(u => u with { OpenWithNitroSenseKey = value });
        if (value)
            nitroSenseKey.Start();
        else
            nitroSenseKey.Stop();
    }

    partial void OnIntervalIndexChanged(int value)
    {
        if (_loading || value < 0)
            return;
        _ = session.SetPollIntervalAsync(Intervals[Math.Clamp(value, 0, Intervals.Length - 1)]);
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (value < 0)
            return;
        UpdateUi(u => u with { Theme = value });
        ThemeChanged?.Invoke(Theme);
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (value < 0 || value >= Languages.Count)
            return;
        var choice = value == 0 ? null : Languages[value].Tag;
        LanguageRestartNeeded = AppLanguage.Resolve(choice) != AppLanguage.Current;
        UpdateUi(u => u with { Language = choice });
    }

    [RelayCommand]
    private static void Restart() => App.Current.Restart();

    partial void OnOperatingModesOnChanged(bool value)
    {
        if (_loading)
            return;
        // Store a choice only where it differs from what this model does by default.
        var choice = value == session.Detected.HasOperatingModes ? (bool?)null : value;
        _ = session.SetOverridesAsync(session.Settings.Overrides with { OperatingModes = choice });
    }

    private static string DescribeOperatingModes(DeviceCapabilities detected)
    {
        var modes = detected.FirmwareOperatingModes.Count > 0 ? detected.FirmwareOperatingModes : detected.OperatingModes;
        var listed = string.Join(Strings.Get("ListSeparator"), modes.Select(Names.OperatingMode));
        var alternative = Strings.Get(detected.FirmwareCoolBoost ? "Settings_OperatingModes_CoolBoost" : "Settings_OperatingModes_Default");
        return Strings.Format(detected.HasOperatingModes ? "Settings_OperatingModes_Supported" : "Settings_OperatingModes_Unused", listed, alternative);
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        var package = new DataPackage();
        package.SetText(Diagnostics);
        Clipboard.SetContent(package);
        notifications.Show(Strings.Get("Notice_Diagnostics_Title"), Strings.Get("Notice_DiagnosticsCopied"), InfoBarSeverity.Success);
    }

    [RelayCommand]
    private static void OpenServiceLogs() => OpenFolder(ServiceLogDirectory);

    [RelayCommand]
    private static void OpenAppLogs() => OpenFolder(AppLogDirectory);

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void UpdateUi(Func<UiSettings, UiSettings> change)
    {
        if (!_loading)
            settings.Update(s => s with { Ui = change(s.Ui) });
    }
}
