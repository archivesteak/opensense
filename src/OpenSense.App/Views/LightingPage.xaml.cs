using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenSense.App.ViewModels;
using Windows.UI;

namespace OpenSense.App.Views;

public sealed partial class LightingPage : Page
{
    public LightingPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<LightingViewModel>();
        InitializeComponent();
    }

    public LightingViewModel ViewModel { get; }

    /// <summary>Swatches are shared by "all zones" and "effect colour"; the owning repeater's Tag says which.</summary>
    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Color color } element)
            return;
        var owner = FindRepeater(element);
        if ((owner?.Tag as string) == "effect")
            ViewModel.EffectColor = color;
        else
            ViewModel.SetAllZonesCommand.Execute(color);
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
