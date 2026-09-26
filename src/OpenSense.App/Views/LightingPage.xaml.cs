using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenSense.App.ViewModels;
using Windows.UI;

namespace OpenSense.App.Views;

public sealed partial class LightingPage : Page
{
    private bool _fillingDevices;

    public LightingPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<LightingViewModel>();
        InitializeComponent();
        ViewModel.Devices.CollectionChanged += (_, _) => FillDeviceBar();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LightingViewModel.DeviceIndex))
                SelectDevice();
        };
        FillDeviceBar();
    }

    public LightingViewModel ViewModel { get; }

    /// <summary>No more columns than zones, so on a wide window the zones stretch instead of leaving empty columns.</summary>
    public static int ZoneColumns(int zones) => Math.Max(zones, 1);

    /// <summary>SelectorBar takes no items source, so its items follow <see cref="LightingViewModel.Devices"/> here.</summary>
    private void FillDeviceBar()
    {
        _fillingDevices = true;
        DeviceBar.Items.Clear();
        foreach (var device in ViewModel.Devices)
            DeviceBar.Items.Add(new SelectorBarItem { Text = device.Name });
        SelectDevice();
        _fillingDevices = false;
    }

    private void SelectDevice()
    {
        var index = ViewModel.DeviceIndex;
        var item = index >= 0 && index < DeviceBar.Items.Count ? DeviceBar.Items[index] : null;
        if (DeviceBar.SelectedItem != item)
            DeviceBar.SelectedItem = item;
    }

    private void OnDeviceSelected(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_fillingDevices || sender.SelectedItem is not { } item)
            return;
        var index = sender.Items.IndexOf(item);
        if (index >= 0)
            ViewModel.DeviceIndex = index;
    }

    /// <summary>Swatches are shared by "all zones", "effect colour" and the per-key editor; the owning repeater's Tag says which.</summary>
    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Color color } element)
            return;
        switch (FindRepeater(element)?.Tag as string)
        {
            case "effect":
                ViewModel.EffectColor = color;
                break;
            case "keys":
                ViewModel.PaintKeysCommand.Execute(color);
                break;
            default:
                ViewModel.SetAllZonesCommand.Execute(color);
                break;
        }
    }

    private static ItemsRepeater? FindRepeater(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ItemsRepeater repeater)
                return repeater;
        }
        return null;
    }
}
