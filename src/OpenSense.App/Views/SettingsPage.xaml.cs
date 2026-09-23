using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.ViewModels;

namespace OpenSense.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        Updates = App.Current.Services.GetRequiredService<UpdateViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    public UpdateViewModel Updates { get; }

    public static string VersionText(string version) => $"Version {version}";
}
