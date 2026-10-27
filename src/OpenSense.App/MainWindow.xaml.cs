using CommunityToolkit.WinUI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.Storage.Pickers;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.App.ViewModels;
using OpenSense.App.Views;
using OpenSense.Core.Hardware;
using Windows.Graphics;

namespace OpenSense.App;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Pages = new()
    {
        ["dashboard"] = typeof(DashboardPage),
        ["fans"] = typeof(FansPage),
        ["lighting"] = typeof(LightingPage),
        ["system"] = typeof(SystemPage),
        ["settings"] = typeof(SettingsPage),
    };

    private readonly SettingsService _settings;
    private readonly ToastService _toasts;

    public MainWindow(ShellViewModel shell, UpdateViewModel updates, SettingsService settings, SettingsViewModel settingsPage, SystemViewModel system,
        BatteryViewModel battery, StartupViewModel startup, GpuClocksViewModel gpuClocks, BiosSettingsViewModel bios, NavigationService navigation,
        ToastService toasts)
    {
        Shell = shell;
        Updates = updates;
        _settings = settings;
        _toasts = toasts;
        InitializeComponent();

        RootGrid.Language = AppLanguage.Current;
        RootGrid.FlowDirection = LayoutDirection;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBar.Loaded += (_, _) => MatchNavigationText();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "OpenSense.ico"));
        PlaceWindow();

        ApplyTheme(settingsPage.Theme);
        settingsPage.ThemeChanged += ApplyTheme;
        system.ConfirmGpuSwitch = ConfirmGpuSwitchAsync;
        battery.ConfirmCalibration = ConfirmCalibrationAsync;
        startup.PickPicture = PickPictureAsync;
        startup.ConfirmLogo = ConfirmBootLogoAsync;
        gpuClocks.ConfirmOverclock = ConfirmOverclockAsync;
        bios.AskPassword = AskBiosPasswordAsync;
        bios.AskNewPassword = AskNewBiosPasswordAsync;
        bios.ConfirmDangerous = ConfirmDangerousBiosAsync;
        bios.ConfirmPassword = ConfirmBiosPasswordAsync;
        settingsPage.ConfirmDangerousBios = ConfirmDangerousBiosSettingsAsync;
        bios.ConfirmDefaults = ConfirmBiosDefaultsAsync;

        AppWindow.Closing += OnClosing;
        navigation.Requested += NavigateTo;
        ContentFrame.NavigationFailed += (_, e) =>
        {
            Serilog.Log.Error(e.Exception, "Navigation to {Page} failed", e.SourcePageType.Name);
            e.Handled = true;
            // Say so, rather than leave the last page (or nothing) under the selected item.
            _failedPage = e.SourcePageType;
            NavigationFailedBar.Title = Strings.Get("Navigation_FailedTitle");
            NavigationFailedBar.Message = e.Exception.Message;
            NavigationRetryButton.Content = Strings.Get("Navigation_Retry");
            NavigationFailedBar.IsOpen = true;
        };
        ContentFrame.Navigated += (_, _) => NavigationFailedBar.IsOpen = false;
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.IsReady) && shell.IsReady)
                SelectInitialPage();
        };
    }

    public ShellViewModel Shell { get; }

    public UpdateViewModel Updates { get; }

    /// <summary>Right to left for Arabic, Hebrew and Persian.</summary>
    private static FlowDirection LayoutDirection => AppLanguage.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private void PlaceWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 900;
            presenter.PreferredMinimumHeight = 640;
        }

        var scale = Windows.Win32.PInvoke.GetDpiForWindow(new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96.0;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(1220 * scale), work.Width);
        var height = Math.Min((int)(860 * scale), work.Height);
        AppWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
    }

    private void ApplyTheme(ElementTheme theme)
    {
        RootGrid.RequestedTheme = theme;
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (App.Current.IsExiting)
            return;
        if (_settings.Current.Ui.CloseToTray && Shell.IsReady)
        {
            args.Cancel = true;
            AppWindow.Hide();
            // Once, so that a window that vanishes isn't a surprise.
            if (!_settings.Current.Ui.TrayHintShown)
            {
                _settings.Update(s => s with { Ui = s.Ui with { TrayHintShown = true } });
                _toasts.Show(Strings.Get("Tray_Hint_Title"), Strings.Get("Tray_Hint_Message"), "trayhint");
            }
            return;
        }
        args.Cancel = true;
        App.Current.Quit();
    }

    private void SelectInitialPage() => NavigateTo(App.Current.Options.Page?.ToLowerInvariant() ?? "dashboard");

    public void NavigateTo(string page)
    {
        Serilog.Log.Debug("Navigate requested {Page}", page);
        if (page == "settings")
        {
            if (Nav.SettingsItem is { } settingsItem)
            {
                Nav.SelectedItem = settingsItem;
                return;
            }
            // The built-in Settings item only exists once the NavigationView has laid out; select it then.
            ContentFrame.Navigate(typeof(SettingsPage));
            void SelectWhenReady(object? sender, object e)
            {
                if (Nav.SettingsItem is not { } item)
                    return;
                Nav.LayoutUpdated -= SelectWhenReady;
                Nav.SelectedItem = item;
            }
            Nav.LayoutUpdated += SelectWhenReady;
            return;
        }
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == page && i.Visibility == Visibility.Visible)
            ?? Nav.MenuItems[0];
    }

    private void OnNavigationChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.IsSettingsSelected ? "settings" : (args.SelectedItem as NavigationViewItem)?.Tag as string;
        Serilog.Log.Debug("Navigation selected {Page}", tag);
        if (tag is not null && Pages.TryGetValue(tag, out var type) && ContentFrame.CurrentSourcePageType != type)
            ContentFrame.Navigate(type, null, args.RecommendedNavigationTransitionInfo ?? new EntranceNavigationTransitionInfo());
    }

    private void OnPaneToggleRequested(TitleBar sender, object args) => Nav.IsPaneOpen = !Nav.IsPaneOpen;

    /// <summary>The page that could not be opened last, for <see cref="OnNavigationRetry"/>.</summary>
    private Type? _failedPage;

    private void OnNavigationRetry(object sender, RoutedEventArgs e)
    {
        NavigationFailedBar.IsOpen = false;
        if (_failedPage is { } page)
            ContentFrame.Navigate(page);
    }

    /// <summary>The title bar's template sets the app name in the 12 px caption style; use the navigation items' 14 px.</summary>
    private void MatchNavigationText()
    {
        if (AppTitleBar.FindDescendant("PART_TitleText") is TextBlock title)
            title.Style = (Style)Application.Current.Resources["BodyTextBlockStyle"];
    }

    private void OnNoticeClosed(InfoBar sender, object args)
    {
        if (sender.DataContext is Notice notice)
            Shell.DismissNoticeCommand.Execute(notice);
    }

    private async Task<bool?> ConfirmGpuSwitchAsync(GpuMode mode)
    {
        var dialog = Dialog(
            Strings.Get(mode == GpuMode.Discrete ? "GpuSwitch_DiscreteTitle" : mode == GpuMode.Automatic ? "GpuSwitch_AutomaticTitle" : "GpuSwitch_HybridTitle"),
            Strings.Get(mode == GpuMode.Discrete ? "GpuSwitch_DiscreteMessage" : mode == GpuMode.Automatic ? "GpuSwitch_AutomaticMessage" : "GpuSwitch_HybridMessage"));
        dialog.PrimaryButtonText = Strings.Get("GpuSwitch_RestartNow");
        dialog.SecondaryButtonText = Strings.Get("GpuSwitch_RestartLater");
        dialog.DefaultButton = ContentDialogButton.Secondary;
        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => true,
            ContentDialogResult.Secondary => false,
            _ => null,
        };
    }

    private async Task<bool> ConfirmCalibrationAsync()
    {
        var dialog = Dialog(Strings.Get("Calibration_Confirm_Title"), Strings.Get("Calibration_Confirm_Message"));
        dialog.PrimaryButtonText = Strings.Get("Calibration_Confirm_Start");
        dialog.DefaultButton = ContentDialogButton.Primary;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmOverclockAsync()
    {
        var dialog = Dialog(Strings.Get("GpuClocks_Confirm_Title"), Strings.Get("GpuClocks_Confirm_Message"));
        dialog.PrimaryButtonText = Strings.Get("GpuClocks_Confirm_Overclock");
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>The BIOS supervisor password, which the BIOS asks for; null when the user gives up.</summary>
    private async Task<string?> AskBiosPasswordAsync(bool wrong)
    {
        var password = new PasswordBox { MaxLength = BiosProtocol.MaxLength };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = Strings.Get(wrong ? "BiosPassword_Wrong" : "BiosPassword_Message"), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(password);

        var dialog = Dialog(Strings.Get("BiosPassword_Title"), content);
        dialog.PrimaryButtonText = Strings.Get("BiosPassword_Continue");
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.IsPrimaryButtonEnabled = false;
        password.PasswordChanged += (_, _) => dialog.IsPrimaryButtonEnabled = password.Password.Length > 0;
        dialog.Opened += (_, _) => password.Focus(FocusState.Programmatic);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? password.Password : null;
    }

    /// <summary>A new password for a BIOS password setting, typed twice; null when the user gives up.</summary>
    /// <param name="maxLength">The longest the BIOS keeps whole.</param>
    private async Task<string?> AskNewBiosPasswordAsync(string setting, int maxLength)
    {
        var first = new PasswordBox { MaxLength = maxLength, Header = Strings.Get("BiosNewPassword_First") };
        var again = new PasswordBox { MaxLength = maxLength, Header = Strings.Get("BiosNewPassword_Again") };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = Strings.Format("BiosNewPassword_Message", setting), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(first);
        content.Children.Add(again);

        var dialog = Dialog(Strings.Get("BiosNewPassword_Title"), content);
        dialog.PrimaryButtonText = Strings.Get("BiosNewPassword_Set");
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.IsPrimaryButtonEnabled = false;
        // Only what the BIOS can take, and the same twice.
        void Check(object sender, RoutedEventArgs e) =>
            dialog.IsPrimaryButtonEnabled = first.Password == again.Password && BiosProtocol.IsPlainText(first.Password);
        first.PasswordChanged += Check;
        again.PasswordChanged += Check;
        dialog.Opened += (_, _) => first.Focus(FocusState.Programmatic);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? first.Password : null;
    }

    /// <summary>Two questions, one after the other, before the dangerous BIOS settings are switched on.</summary>
    private async Task<bool> ConfirmDangerousBiosSettingsAsync()
    {
        var first = Dialog(Strings.Get("BiosDangerousEnable_Title"), Strings.Get("BiosDangerousEnable_Message"));
        first.PrimaryButtonText = Strings.Get("BiosDangerousEnable_Continue");
        first.DefaultButton = ContentDialogButton.Close;
        if (await first.ShowAsync() != ContentDialogResult.Primary)
            return false;

        var second = Dialog(Strings.Get("BiosDangerousSure_Title"), Strings.Get("BiosDangerousSure_Message"));
        second.PrimaryButtonText = Strings.Get("BiosDangerousSure_Enable");
        second.DefaultButton = ContentDialogButton.Close;
        return await second.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>The last question before a BIOS password is saved.</summary>
    private async Task<bool> ConfirmBiosPasswordAsync(string setting)
    {
        var dialog = Dialog(Strings.Get("BiosPasswordSave_Title"), Strings.Format("BiosPasswordSave_Message", setting));
        dialog.PrimaryButtonText = Strings.Get("BiosPasswordSave_Save");
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmDangerousBiosAsync(string name, string value)
    {
        var dialog = Dialog(Strings.Get("BiosDanger_Title"), Strings.Format("BiosDanger_Message", name, value));
        dialog.PrimaryButtonText = Strings.Get("BiosDanger_Change");
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmBiosDefaultsAsync(bool user)
    {
        var dialog = Dialog(
            Strings.Get(user ? "BiosUserDefaults_Title" : "BiosDefaults_Title"),
            Strings.Get(user ? "BiosUserDefaults_Message" : "BiosDefaults_Message"));
        dialog.PrimaryButtonText = Strings.Get("BiosDefaults_Load");
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>A GIF or JPEG file for the boot logo; null when cancelled.</summary>
    private async Task<string?> PickPictureAsync()
    {
        // The Windows App SDK picker, which also works in an elevated (portable) copy.
        var picker = new FileOpenPicker(AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail,
        };
        foreach (var type in new[] { ".jpg", ".jpeg", ".gif" })
            picker.FileTypeFilter.Add(type);
        return (await picker.PickSingleFileAsync())?.Path;
    }

    private async Task<bool> ConfirmBootLogoAsync(byte[] picture, bool turnOnAnimation)
    {
        var content = new StackPanel { Spacing = 12 };
        if (await Pictures.FromBytesAsync(picture, 480) is { } preview)
            content.Children.Add(new Image { Source = preview, MaxHeight = 240, HorizontalAlignment = HorizontalAlignment.Left });
        var message = turnOnAnimation ? Strings.Get("BootLogo_Confirm_MessageWithAnimation") : Strings.Get("BootLogo_Confirm_Message");
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var dialog = Dialog(Strings.Get("BootLogo_Confirm_Title"), content);
        dialog.PrimaryButtonText = Strings.Get("BootLogo_Confirm_Use");
        dialog.DefaultButton = ContentDialogButton.Primary;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>A dialog over this window, with Cancel as its close button.</summary>
    private ContentDialog Dialog(string title, object content) => new()
    {
        XamlRoot = Content.XamlRoot,
        Title = title,
        Content = content,
        CloseButtonText = Strings.Get("GpuSwitch_Cancel"),
        RequestedTheme = RootGrid.ActualTheme,
        // Dialogs open outside the window's content, so they don't inherit these.
        Language = RootGrid.Language,
        FlowDirection = RootGrid.FlowDirection,
    };
}
