using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Services;

namespace OpenSense.App.ViewModels;

/// <summary>Startup state, device name, page availability and notices.</summary>
public sealed partial class ShellViewModel(
    DispatcherQueue dispatcher,
    DeviceSession session,
    NotificationService notifications,
    MonitorViewModel monitor,
    FanControlViewModel fans,
    LightingViewModel lighting,
    SystemViewModel system,
    SettingsViewModel settings) : ObservableObject
{
    private bool _subscribed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(IsStarting), nameof(ShowProblem), nameof(ProblemTitle), nameof(ProblemMessage),
        nameof(CanRetryElevation), nameof(CanStartService))]
    public partial SessionState State { get; set; } = SessionState.Starting;

    [ObservableProperty]
    public partial bool LightingAvailable { get; set; }

    public bool IsReady => State == SessionState.Ready;

    public bool IsStarting => State == SessionState.Starting;

    public bool ShowProblem => State is SessionState.NeedsElevation or SessionState.ServiceUnavailable or SessionState.Unsupported or SessionState.Failed;

    public bool CanRetryElevation => State == SessionState.NeedsElevation;

    public bool CanStartService => State == SessionState.ServiceUnavailable;

    public string ProblemTitle => State switch
    {
        SessionState.NeedsElevation => "Administrator rights needed",
        SessionState.ServiceUnavailable => "The OpenSense service isn't running",
        SessionState.Unsupported => "No Acer gaming firmware found",
        _ => "OpenSense could not start",
    };

    public string ProblemMessage => State switch
    {
        SessionState.NeedsElevation => "This portable copy talks to the laptop's fan and lighting controls directly, which needs administrator rights. Restart it as administrator, or install OpenSense to run without them.",
        SessionState.ServiceUnavailable => "OpenSense controls the laptop through a background service, which is stopped. Start it (Windows asks for permission), or reinstall OpenSense if it keeps stopping.",
        SessionState.Unsupported => "This PC does not expose Acer's gaming WMI interface (AcerGamingFunction). OpenSense supports Acer Nitro and Predator laptops.",
        _ => session.Error ?? "An unexpected error occurred. The log folder has details.",
    };

    public ObservableCollection<Notice> Notices => notifications.Notices;

    public async Task InitializeAsync()
    {
        State = SessionState.Starting;
        await session.InitializeAsync();
        if (session.State != SessionState.Ready)
        {
            State = session.State;
            return;
        }

        Subscribe();
        AttachAll();
        State = SessionState.Ready; // after attaching, so every page and nav item is ready to show
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;
        _subscribed = true;
        session.Notice += notice => notifications.Show(notice.Title, notice.Message, InfoBarSeverity.Warning, notice.Important);
        session.Changed += change => dispatcher.TryEnqueue(() =>
        {
            AttachAll();
            if (change == SessionChange.Rebuilt)
                notifications.Show("Capabilities updated", "OpenSense restarted fan control with the new settings.", InfoBarSeverity.Informational);
        });
        session.ConnectionChanged += connected =>
        {
            if (connected)
                notifications.Show("Service", "Reconnected to the OpenSense service.", InfoBarSeverity.Success);
            else
                notifications.Show("Service", "Lost the connection to the OpenSense service. Reconnecting…", InfoBarSeverity.Warning);
        };
    }

    private void AttachAll()
    {
        monitor.Attach();
        fans.Attach();
        lighting.Attach();
        system.Attach();
        settings.Attach();
        LightingAvailable = lighting.Available;
    }

    [RelayCommand]
    private static void RetryElevation() => App.Current.RelaunchElevated();

    [RelayCommand]
    private async Task StartServiceAsync()
    {
        var started = await Task.Run(() => Program.TryRunElevated("--start-service", wait: true));
        if (!started)
        {
            notifications.Show("Service", "The OpenSense service did not start. The service log in %ProgramData%\\OpenSense\\logs has details.", InfoBarSeverity.Error);
            return;
        }
        await InitializeAsync();
    }

    [RelayCommand]
    private void DismissNotice(Notice notice) => notifications.Dismiss(notice);
}
