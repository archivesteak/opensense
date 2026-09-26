using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Localization;
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
    BatteryViewModel battery,
    StartupViewModel startup,
    GpuClocksViewModel gpuClocks,
    SettingsViewModel settings,
    ToastService toasts) : ObservableObject
{
    /// <summary>Operating-mode toasts replace each other, and leave the notification centre after a minute.</summary>
    private const string ModeToastTag = "opmode";
    private static readonly TimeSpan ModeToastLifetime = TimeSpan.FromMinutes(1);

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
        SessionState.NeedsElevation => Strings.Get("Problem_NeedsElevation_Title"),
        SessionState.ServiceUnavailable => Strings.Get("Problem_ServiceUnavailable_Title"),
        SessionState.Unsupported => Strings.Get("Problem_Unsupported_Title"),
        _ => Strings.Get("Problem_Failed_Title"),
    };

    public string ProblemMessage => State switch
    {
        SessionState.NeedsElevation => Strings.Get("Problem_NeedsElevation_Message"),
        SessionState.ServiceUnavailable => Strings.Get("Problem_ServiceUnavailable_Message"),
        SessionState.Unsupported => Strings.Get("Problem_Unsupported_Message"),
        _ => session.Error ?? Strings.Get("Problem_Failed_Message"),
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
        session.Notice += notice =>
        {
            var (title, message) = Names.Notice(notice);
            if (Names.IsModeToast(notice.Kind))
            {
                // Like Acer's on-screen display for the Mode key and the adapter: a toast, not a banner.
                dispatcher.TryEnqueue(() => toasts.Show(title, message, ModeToastTag, ModeToastLifetime));
                return;
            }
            var severity = notice.Kind == Core.Control.NoticeKind.CalibrationFinished ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            notifications.Show(title, message, severity, notice.Important);
        };
        session.Changed += _ => dispatcher.TryEnqueue(AttachAll);
        session.ConnectionChanged += connected =>
        {
            // Back again is not news: the "reconnecting" notice just goes away.
            if (connected)
                notifications.Dismiss(Strings.Get("Notice_Disconnected"));
            else
                notifications.Show(Strings.Get("Notice_Service_Title"), Strings.Get("Notice_Disconnected"), InfoBarSeverity.Warning);
        };
    }

    private void AttachAll()
    {
        monitor.Attach();
        fans.Attach();
        lighting.Attach();
        system.Attach();
        battery.Attach();
        startup.Attach();
        gpuClocks.Attach();
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
            notifications.Show(Strings.Get("Notice_Service_Title"), Strings.Get("Notice_ServiceStartFailed"), InfoBarSeverity.Error);
            return;
        }
        await InitializeAsync();
    }

    [RelayCommand]
    private void DismissNotice(Notice notice) => notifications.Dismiss(notice);
}
