using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.ViewModels;

namespace OpenSense.App.Views;

public sealed partial class SystemPage : Page
{
    public SystemPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SystemViewModel>();
        Battery = App.Current.Services.GetRequiredService<BatteryViewModel>();
        Startup = App.Current.Services.GetRequiredService<StartupViewModel>();
        GpuClocks = App.Current.Services.GetRequiredService<GpuClocksViewModel>();
        Fans = App.Current.Services.GetRequiredService<FanControlViewModel>();
        InitializeComponent();
    }

    /// <summary>The Mode key's setting is part of the operating modes.</summary>
    public FanControlViewModel Fans { get; }

    public SystemViewModel ViewModel { get; }

    public BatteryViewModel Battery { get; }

    public StartupViewModel Startup { get; }

    public GpuClocksViewModel GpuClocks { get; }

    /// <summary>A section shows when it has either setting.</summary>
    public static Visibility Either(bool first, bool second) => first || second ? Visibility.Visible : Visibility.Collapsed;
}
