using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
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

    public static double ManualOpacity(bool manual) => manual ? 1 : 0.45;

    public static int RpmValue(int? rpm) => rpm ?? 0;

    public static double DutyValue(int? duty) => duty ?? double.NaN;

    public static string DutyLabel(string duty) => Strings.Format("Fans_DutyLabel", duty);

    public static string TemperatureLabel(double celsius, bool fahrenheit) => Units.TemperatureWithUnit(celsius, fahrenheit);

    public static string PercentLabel(double value) => Units.Percent(value);

    public static string DegreesLabel(double value) => string.Create(CultureInfo.CurrentCulture, $"{value:0} °");
}
