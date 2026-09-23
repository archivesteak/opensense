using System.Diagnostics;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public static IReadOnlyList<string> IntervalNames { get; } = ["0.5 s", "1 s", "2 s"];

    public static IReadOnlyList<string> ThemeNames { get; } = ["Use system setting", "Light", "Dark"];

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

        var detected = session.Detected;
        OperatingModesSwitchAvailable = detected.FirmwareOperatingModes.Count > 0 || detected.HasOperatingModes;
        OperatingModesOn = session.Capabilities.HasOperatingModes;
        OperatingModesDescription = DescribeOperatingModes(detected);

        Diagnostics = detected.Diagnostics;
        LaptopModel = session.DeviceName;
        BiosVersion = session.BiosVersion is { } bios ? $"BIOS {bios}" : "BIOS version unknown";
        var sources = session.TemperatureSources;
        TemperatureSources = $"CPU: {sources.Cpu}.\nGPU: {sources.Gpu}.";
        PawnIOMissing = sources.PawnIOMissing;
        ModeDescription = session.IsPortable ? "Portable copy: runs as administrator and controls the laptop itself while it is open."
            : "Installed: the OpenSense service controls the laptop from startup, whether or not this window is open.";
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
            notifications.Show("Start with Windows", $"Could not update the startup entry: {ex.Message}", InfoBarSeverity.Error);
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
        var listed = string.Join(", ", modes);
        var alternative = detected.FirmwareCoolBoost
            ? "CoolBoost is available while they are off."
            : "While they are off the laptop stays in its default mode.";
        return detected.HasOperatingModes
            ? $"{listed}, as Acer's software offers them on this model. {alternative}"
            : $"Your firmware lists {listed}, but Acer's software doesn't use them on this model and they may change nothing. {alternative}";
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        var package = new DataPackage();
        package.SetText(Diagnostics);
        Clipboard.SetContent(package);
        notifications.Show("Diagnostics", "Copied to the clipboard.", InfoBarSeverity.Success);
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
