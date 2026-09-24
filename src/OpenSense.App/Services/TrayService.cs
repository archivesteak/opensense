using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenSense.App.Localization;
using OpenSense.App.ViewModels;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.App.Services;

/// <summary>Notification-area icon: the OpenSense icon, a tooltip with the temperatures, and quick controls.</summary>
public sealed class TrayService(MonitorViewModel monitor, FanControlViewModel fans, NotificationService notifications) : IDisposable
{
    private TaskbarIcon? _icon;
    private readonly List<RadioMenuFlyoutItem> _modeItems = [];
    private MenuFlyoutSubItem? _operatingModeMenu;

    public void Initialize()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "OpenSense",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/OpenSense.ico")),
            ContextMenuMode = ContextMenuMode.SecondWindow,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(() => App.Current.ShowMainWindow()),
            ContextFlyout = BuildMenu(),
        };
        _icon.ForceCreate(false);

        monitor.PropertyChanged += OnMonitorChanged;
        fans.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FanControlViewModel.ModeIndex) or nameof(FanControlViewModel.OperatingModeIndex)
                or nameof(FanControlViewModel.OperatingModesAvailable))
                SyncMenu();
        };
        notifications.Important += notice =>
        {
            if (!App.Current.IsExiting)
                _icon?.ShowNotification(notice.Title, notice.Message, notice.Severity == InfoBarSeverity.Informational
                    ? H.NotifyIcon.Core.NotificationIcon.Info
                    : H.NotifyIcon.Core.NotificationIcon.Warning);
        };
    }

    private MenuFlyout BuildMenu()
    {
        var menu = new MenuFlyout();
        if (AppLanguage.IsRightToLeft)
        {
            // The menu opens in its own window, outside the main window's right-to-left layout.
            var presenter = new Style(typeof(MenuFlyoutPresenter));
            presenter.Setters.Add(new Setter(FrameworkElement.FlowDirectionProperty, FlowDirection.RightToLeft));
            menu.MenuFlyoutPresenterStyle = presenter;
        }
        menu.Items.Add(Item(Strings.Get("Tray_Open"), () => App.Current.ShowMainWindow()));
        menu.Items.Add(new MenuFlyoutSeparator());

        var fanMenu = new MenuFlyoutSubItem { Text = Strings.Get("Tray_FanMode") };
        for (var i = 0; i < FanControlViewModel.ModeNames.Count; i++)
        {
            var index = i;
            var item = new RadioMenuFlyoutItem { Text = FanControlViewModel.ModeNames[i], GroupName = "fanmode" };
            item.Click += (_, _) => fans.ModeIndex = index;
            _modeItems.Add(item);
            fanMenu.Items.Add(item);
        }
        menu.Items.Add(fanMenu);

        _operatingModeMenu = new MenuFlyoutSubItem { Text = Strings.Get("Tray_OperatingMode") };
        menu.Items.Add(_operatingModeMenu);

        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item(Strings.Get("Tray_Exit"), () => App.Current.Quit()));
        menu.Opening += (_, _) => SyncMenu();
        SyncMenu();
        return menu;
    }

    private static MenuFlyoutItem Item(string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void SyncMenu()
    {
        for (var i = 0; i < _modeItems.Count; i++)
            _modeItems[i].IsChecked = i == fans.ModeIndex;

        if (_operatingModeMenu is not null)
        {
            _operatingModeMenu.Visibility = fans.OperatingModesAvailable ? Visibility.Visible : Visibility.Collapsed;
            _operatingModeMenu.Items.Clear();
            for (var i = 0; i < fans.OperatingModes.Count; i++)
            {
                var index = i;
                var item = new RadioMenuFlyoutItem { Text = fans.OperatingModes[i].Name, GroupName = "opmode", IsChecked = i == fans.OperatingModeIndex };
                item.Click += (_, _) => fans.OperatingModeIndex = index;
                _operatingModeMenu.Items.Add(item);
            }
        }
    }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MonitorViewModel.Latest) || _icon is null || monitor.Latest is not { } t)
            return;

        var cpu = Names.Chip(FanId.Cpu);
        var gpu = Names.Chip(FanId.Gpu);
        var fanText = string.Join("  ", monitor.Fans.Select(f => $"{Names.Chip(f.Id)} {f.RpmText}"));
        var tooltip = $"OpenSense · {Names.FanMode(t.EffectiveMode)}\n{cpu} {monitor.CpuTemperatureText}  {gpu} {monitor.GpuTemperatureText}\n{fanText}";
        if (_icon.ToolTipText != tooltip)
            _icon.ToolTipText = tooltip;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
