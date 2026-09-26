using System.Diagnostics;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;
using OpenSense.Core.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace OpenSense.App.ViewModels;

/// <summary>App preferences, capability overrides, diagnostics and about.</summary>
public sealed partial class SettingsViewModel(SettingsService settings, DeviceSession session, NotificationService notifications, NitroSenseKey nitroSenseKey,
    OpenShortcut openShortcut)
    : ObservableObject
{
    private static readonly string AppLogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSense", "logs");
    private static readonly string ServiceLogDirectory = Path.Combine(SettingsPaths.MachineDirectory, "logs");

    private bool _loading;

    public static IReadOnlyList<string> ThemeNames { get; } = [Strings.Get("Theme_System"), Strings.Get("Theme_Light"), Strings.Get("Theme_Dark")];

    /// <summary>"Use system setting" (an empty tag), then every translation by its own name.</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } = [new("", Strings.Get("Language_System")), .. AppLanguage.Available];

    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }


    [ObservableProperty]
    public partial bool UseFahrenheit { get; set; }

    [ObservableProperty]
    public partial bool OpenWithNitroSenseKey { get; set; }

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

    /// <summary>A Sunrex per-key keyboard, which may have the MagForce keys' lights (told by the model name, so the user can say otherwise).</summary>
    [ObservableProperty]
    public partial bool MagKeySwitchAvailable { get; set; }

    [ObservableProperty]
    public partial bool MagKeyOn { get; set; }

    [ObservableProperty]
    public partial string LaptopModel { get; set; } = "";

    [ObservableProperty]
    public partial string BiosVersion { get; set; } = "";

    /// <summary>The serial number on the laptop's label; empty when Windows does not say.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSerialNumber))]
    public partial string SerialNumber { get; set; } = "";

    /// <summary>Acer's SNID, worked out from the serial number as Care Center does; empty for a serial not in Acer's format.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSnid))]
    public partial string Snid { get; set; } = "";

    public bool HasSerialNumber => SerialNumber.Length > 0;

    public bool HasSnid => Snid.Length > 0;

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
        StartWithWindows = AutostartService.IsEnabled;
        CloseToTray = ui.CloseToTray;
        UseFahrenheit = ui.UseFahrenheit;
        OpenWithNitroSenseKey = ui.OpenWithNitroSenseKey;
        RecordingShortcut = false;
        Shortcut = ui.OpenShortcut;
        ShortcutHelp = Strings.Get(Shortcut is not null && !openShortcut.IsSet ? "Settings_ShortcutTaken" : "Settings_ShortcutHelp");
        ThemeIndex = ui.Theme;
        LanguageIndex = Math.Max(0, Languages.ToList().FindIndex(l => l.Tag.Length > 0 && string.Equals(l.Tag, ui.Language, StringComparison.OrdinalIgnoreCase)));

        var detected = session.Detected;
        OperatingModesSwitchAvailable = detected.FirmwareOperatingModes.Count > 0 || detected.HasOperatingModes;
        OperatingModesOn = session.Capabilities.HasOperatingModes;
        OperatingModesDescription = DescribeOperatingModes(detected);
        MagKeySwitchAvailable = detected.MagKeyLight is not null;
        MagKeyOn = session.Capabilities.Lights.Any(l => l.Location == LightingLocation.MagKey);

        LaptopModel = session.DeviceName ?? Strings.Get("Settings_UnknownModel");
        BiosVersion = session.BiosVersion is { } bios ? Strings.Format("Settings_Bios", bios) : Strings.Get("Settings_BiosUnknown");
        SerialNumber = session.SerialNumber ?? "";
        Snid = SystemInfo.Snid(session.SerialNumber) ?? "";
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

    partial void OnCloseToTrayChanged(bool value) => UpdateUi(u => u with { CloseToTray = value });

    partial void OnUseFahrenheitChanged(bool value) => UpdateUi(u => u with { UseFahrenheit = value });

    /// <summary>The shortcut that opens OpenSense (<see cref="Services.OpenShortcut"/>); null for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutText), nameof(HasShortcut))]
    public partial KeyShortcut? Shortcut { get; set; }

    /// <summary>The shortcut button is waiting for a key.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutText))]
    public partial bool RecordingShortcut { get; set; }

    [ObservableProperty]
    public partial string ShortcutHelp { get; set; } = "";

    public bool HasShortcut => Shortcut is not null;

    public string ShortcutText =>
        RecordingShortcut ? Strings.Get("Settings_ShortcutRecording")
        : Shortcut is { } shortcut ? KeyNames.Describe(shortcut)
        : Strings.Get("Settings_ShortcutNone");

    [RelayCommand]
    private void RecordShortcut()
    {
        openShortcut.Set(null); // so pressing the current shortcut records it rather than opening the window
        RecordingShortcut = true;
    }

    public void CancelShortcutRecording()
    {
        if (!RecordingShortcut)
            return;
        RecordingShortcut = false;
        openShortcut.Set(Shortcut);
    }

    [RelayCommand]
    private void ClearShortcut() => ApplyShortcut(null);

    /// <summary>
    /// A key pressed while recording: any key, alone or with modifiers. False, and still recording, for a modifier
    /// on its own (the key it goes with is still to come). Esc on its own cancels.
    /// </summary>
    public bool TryRecordShortcut(VirtualKey key, bool control, bool alt, bool shift, bool windows)
    {
        if (!RecordingShortcut || IsModifier(key))
            return false;
        if (key == VirtualKey.Escape && !(control || alt || shift || windows))
        {
            CancelShortcutRecording();
            return true;
        }
        RecordingShortcut = false;
        ApplyShortcut(new KeyShortcut((int)key, control, alt, shift, windows));
        return true;
    }

    private void ApplyShortcut(KeyShortcut? shortcut)
    {
        if (openShortcut.Set(shortcut))
        {
            Shortcut = shortcut;
            ShortcutHelp = Strings.Get("Settings_ShortcutHelp");
            UpdateUi(u => u with { OpenShortcut = shortcut });
        }
        else
        {
            openShortcut.Set(Shortcut); // another app has it: keep the one that worked
            ShortcutHelp = Strings.Get("Settings_ShortcutTaken");
        }
    }

    private static bool IsModifier(VirtualKey key) => key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or VirtualKey.Shift or VirtualKey.LeftShift
        or VirtualKey.RightShift or VirtualKey.LeftWindows or VirtualKey.RightWindows;

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

    partial void OnMagKeyOnChanged(bool value)
    {
        if (_loading)
            return;
        var detected = session.Detected.Lights.Any(l => l.Location == LightingLocation.MagKey);
        _ = session.SetOverridesAsync(session.Settings.Overrides with { MagKey = value == detected ? null : value });
    }

    private static string DescribeOperatingModes(DeviceCapabilities detected)
    {
        var modes = detected.FirmwareOperatingModes.Count > 0 ? detected.FirmwareOperatingModes : detected.OperatingModes;
        var listed = string.Join(Strings.Get("ListSeparator"), modes.Select(Names.OperatingMode));
        var alternative = Strings.Get(detected.FirmwareCoolBoost ? "Settings_OperatingModes_CoolBoost" : "Settings_OperatingModes_Default");
        return Strings.Format(detected.HasOperatingModes ? "Settings_OperatingModes_Supported" : "Settings_OperatingModes_Unused", listed, alternative);
    }

    /// <summary>The Copy button's text: "Copied" for a moment after copying.</summary>
    [ObservableProperty]
    public partial string CopyText { get; set; } = Strings.Get("Settings_Copy/Content");

    private int _copies;

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CopyDiagnosticsAsync()
    {
        var package = new DataPackage();
        package.SetText(await session.GetDiagnosticsAsync());
        Clipboard.SetContent(package);

        // Said on the button rather than with a notice.
        var copy = ++_copies;
        CopyText = Strings.Get("Settings_Copied");
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (copy == _copies)
            CopyText = Strings.Get("Settings_Copy/Content");
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
