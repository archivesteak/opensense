using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
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

    public MainWindow(ShellViewModel shell, UpdateViewModel updates, SettingsService settings, SettingsViewModel settingsPage, SystemViewModel system,
        NavigationService navigation)
    {
        Shell = shell;
        Updates = updates;
        _settings = settings;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "OpenSense.ico"));
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        PlaceWindow();

        ApplyTheme(settingsPage.Theme);
        settingsPage.ThemeChanged += ApplyTheme;
        system.ConfirmGpuSwitch = ConfirmGpuSwitchAsync;

        AppWindow.Closing += OnClosing;
        navigation.Requested += NavigateTo;
        ContentFrame.NavigationFailed += (_, e) =>
        {
            Serilog.Log.Error(e.Exception, "Navigation to {Page} failed", e.SourcePageType.Name);
            e.Handled = true;
        };
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.IsReady) && shell.IsReady)
                SelectInitialPage();
        };
    }

    public ShellViewModel Shell { get; }

    public UpdateViewModel Updates { get; }

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

    private void OnNoticeClosed(InfoBar sender, object args)
    {
        if (sender.DataContext is Notice notice)
            Shell.DismissNoticeCommand.Execute(notice);
    }

    private async Task<bool?> ConfirmGpuSwitchAsync(GpuMode mode)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = mode == GpuMode.Discrete ? "Switch to discrete GPU only?" : "Switch to hybrid graphics?",
            Content = mode == GpuMode.Discrete
                ? "The NVIDIA GPU will drive the display directly for the best gaming performance, at the cost of battery life. Windows must restart to apply this."
                : "The integrated GPU will drive the display and the discrete GPU powers up only when needed, saving battery. Windows must restart to apply this.",
            PrimaryButtonText = "Restart now",
            SecondaryButtonText = "Restart later",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Secondary,
            RequestedTheme = RootGrid.ActualTheme,
        };
        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => true,
            ContentDialogResult.Secondary => false,
            _ => null,
        };
    }
}
