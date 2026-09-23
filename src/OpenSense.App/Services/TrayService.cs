using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenSense.App.ViewModels;
using OpenSense.Core.Control;

namespace OpenSense.App.Services;

/// <summary>Notification-area icon: the OpenSense icon, a tooltip with the temperatures, and quick controls.</summary>
public sealed class TrayService(MonitorViewModel monitor, FanControlViewModel fans, NotificationService notifications) : IDisposable
{
    private TaskbarIcon? _icon;
    private readonly List<RadioMenuFlyoutItem> _modeItems = [];
    private ToggleMenuFlyoutItem? _coolBoostItem;
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
            if (e.PropertyName is nameof(FanControlViewModel.ModeIndex) or nameof(FanControlViewModel.CoolBoost)
                or nameof(FanControlViewModel.OperatingModeIndex) or nameof(FanControlViewModel.OperatingModesAvailable)
                or nameof(FanControlViewModel.CoolBoostAvailable))
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
        menu.Items.Add(Item("Open OpenSense", () => App.Current.ShowMainWindow()));
        menu.Items.Add(new MenuFlyoutSeparator());

        var fanMenu = new MenuFlyoutSubItem { Text = "Fan mode" };
        for (var i = 0; i < FanControlViewModel.ModeNames.Count; i++)
        {
            var index = i;
            var item = new RadioMenuFlyoutItem { Text = FanControlViewModel.ModeNames[i], GroupName = "fanmode" };
            item.Click += (_, _) => fans.ModeIndex = index;
            _modeItems.Add(item);
            fanMenu.Items.Add(item);
        }
        menu.Items.Add(fanMenu);

        _operatingModeMenu = new MenuFlyoutSubItem { Text = "Operating mode" };
        menu.Items.Add(_operatingModeMenu);

        _coolBoostItem = new ToggleMenuFlyoutItem { Text = "CoolBoost" };
        _coolBoostItem.Click += (_, _) => fans.CoolBoost = _coolBoostItem.IsChecked;
        menu.Items.Add(_coolBoostItem);

        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Exit", () => App.Current.Quit()));
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

        if (_coolBoostItem is not null)
        {
            _coolBoostItem.Visibility = fans.CoolBoostAvailable ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            _coolBoostItem.IsChecked = fans.CoolBoost;
        }

        if (_operatingModeMenu is not null)
        {
            _operatingModeMenu.Visibility = fans.OperatingModesAvailable ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
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

        var mode = t.EffectiveMode switch
        {
            FanControlMode.Max => "Max",
            FanControlMode.Custom => "Custom",
            FanControlMode.Curve => "Curve",
            _ => "Auto",
        };
        var fanText = string.Join("  ", monitor.Fans.Select(f => $"{f.Name.Replace(" fan", "", StringComparison.Ordinal)} {f.RpmText}"));
        var tooltip = $"OpenSense — {mode}\nCPU {monitor.CpuTemperatureText}  GPU {monitor.GpuTemperatureText}\n{fanText}";
        if (_icon.ToolTipText != tooltip)
            _icon.ToolTipText = tooltip;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
