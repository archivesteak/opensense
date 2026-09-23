using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.ViewModels;

namespace OpenSense.App.Views;

public sealed partial class SystemPage : Page
{
    public SystemPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SystemViewModel>();
        InitializeComponent();
    }

    public SystemViewModel ViewModel { get; }
}
