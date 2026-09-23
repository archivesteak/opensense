using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.App.ViewModels;
using Windows.UI;

namespace OpenSense.App.Views;

public sealed partial class DashboardPage : Page
{
    private static readonly Color UnknownTemperature = Color.FromArgb(0x66, 0x80, 0x80, 0x80);

    public DashboardPage()
    {
        var services = App.Current.Services;
        Monitor = services.GetRequiredService<MonitorViewModel>();
        Fans = services.GetRequiredService<FanControlViewModel>();
        System = services.GetRequiredService<SystemViewModel>();
        InitializeComponent();
    }

    public MonitorViewModel Monitor { get; }

    public FanControlViewModel Fans { get; }

    public SystemViewModel System { get; }

    public static int FanRpm(int? rpm) => rpm ?? 0;

    public static double FanDuty(int? duty) => duty ?? double.NaN;

    public static string RpmDetail(string duty) => Strings.Format("Dashboard_RpmDetail", duty);

    public static Visibility Present(object? item) => item is null ? Visibility.Collapsed : Visibility.Visible;

    public static SolidColorBrush TemperatureBrush(double celsius) =>
        new(double.IsNaN(celsius) ? UnknownTemperature : TemperatureScale.ColorFor(celsius));

    public static string LoadDetail(string load, string name) => Strings.Format("Dashboard_LoadDetail", load, name);

    public static string GpuDetail(bool asleep, string load, string name) =>
        asleep ? Strings.Format("Dashboard_AsleepDetail", name) : LoadDetail(load, name);

    public static string PowerSource(bool onAc) => Strings.Get(onAc ? "PowerSource_PluggedIn" : "PowerSource_Battery");

    private void OnOpenFans(object sender, RoutedEventArgs e) =>
        App.Current.Services.GetRequiredService<NavigationService>().Navigate("fans");
}
