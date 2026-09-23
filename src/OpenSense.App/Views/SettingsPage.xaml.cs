using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Localization;
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

    public static string VersionText(string version) => Strings.Format("Settings_Version", version);

    /// <summary>The language a language picker entry is written in (the "use system setting" entry is in the app's).</summary>
    public static string TextLanguage(string tag) => tag.Length > 0 ? tag : AppLanguage.Current;
}
