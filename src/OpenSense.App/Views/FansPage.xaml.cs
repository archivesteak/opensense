using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.ViewModels;

namespace OpenSense.App.Views;

public sealed partial class FansPage : Page
{
    public FansPage()
    {
        Fans = App.Current.Services.GetRequiredService<FanControlViewModel>();
        Monitor = App.Current.Services.GetRequiredService<MonitorViewModel>();
        InitializeComponent();
    }

    public FanControlViewModel Fans { get; }

    public MonitorViewModel Monitor { get; }

    public static int RpmValue(int? rpm) => rpm ?? 0;

    public static double BoostValue(int? boost) => boost ?? double.NaN;
}
