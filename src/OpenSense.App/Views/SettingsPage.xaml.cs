using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenSense.App.Localization;
using OpenSense.App.ViewModels;
using Windows.System;
using Windows.Win32;

namespace OpenSense.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        Updates = App.Current.Services.GetRequiredService<UpdateViewModel>();
        InitializeComponent();
        // Recording takes keys at the page, before any control reacts to them (Tab, Space, arrows).
        PreviewKeyDown += OnShortcutKeyDown;
    }

    public SettingsViewModel ViewModel { get; }

    public UpdateViewModel Updates { get; }

    public static string VersionText(string version) => Strings.Format("Settings_Version", version);

    /// <summary>The language a language picker entry is written in (the "use system setting" entry is in the app's).</summary>
    public static string TextLanguage(string tag) => tag.Length > 0 ? tag : AppLanguage.Current;

    private void OnShortcutKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.RecordingShortcut)
            return;
        // While recording, keys are for the shortcut, not for moving focus or pressing the button.
        e.Handled = true;
        ViewModel.TryRecordShortcut(e.Key, IsDown(VirtualKey.Control), IsDown(VirtualKey.Menu), IsDown(VirtualKey.Shift),
            IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows));
    }

    private void OnShortcutLostFocus(object sender, RoutedEventArgs e) => ViewModel.CancelShortcutRecording();

    /// <summary>The physical key state; WinUI's per-thread key state reported held modifiers as up.</summary>
    private static bool IsDown(VirtualKey key) => (PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0;
}
