using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Updates;

namespace OpenSense.App.ViewModels;

/// <summary>
/// Update checks against GitHub Releases, scheduled like Prism Launcher's: once a day, remembered across
/// restarts, with "skip this version". Offered in a banner like Parabolic's, with download progress.
/// </summary>
public sealed partial class UpdateViewModel : ObservableObject
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    /// <summary>Let the app finish starting before the first check of the day.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);

    private readonly GitHubUpdater _updater;
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly ILogger<UpdateViewModel> _log;
    private readonly DispatcherQueueTimer _timer;

    public UpdateViewModel(GitHubUpdater updater, SettingsService settings, NotificationService notifications,
        DispatcherQueue dispatcher, ILogger<UpdateViewModel> log)
    {
        _updater = updater;
        _settings = settings;
        _notifications = notifications;
        _log = log;
        _timer = dispatcher.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += async (_, _) => await CheckAsync(userInitiated: false);
        CheckAutomatically = settings.Current.Updates.CheckAutomatically;
        Status = LastCheckText();
    }

    public InstallKind Kind { get; } = UpdateInstaller.DetectKind();

    public string CurrentVersionText => $"OpenSense {_updater.CurrentVersion.ToString(3)}";

    [ObservableProperty]
    public partial bool CheckAutomatically { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(BannerTitle), nameof(BannerMessage))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(SkipCommand), nameof(OpenReleasePageCommand))]
    public partial AvailableUpdate? Available { get; set; }

    /// <summary>The banner in the main window; closing it means "later".</summary>
    [ObservableProperty]
    public partial bool BannerOpen { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerMessage))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(SkipCommand), nameof(CheckNowCommand))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerMessage))]
    public partial double DownloadProgress { get; set; }

    /// <summary>One line for the Settings page.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "";

    public bool HasUpdate => Available is not null;

    public string BannerTitle => Available is { } u ? Strings.Format("Update_BannerTitle", u.Version.ToString(3)) : "";

    public string BannerMessage => Available is not { } u ? ""
        : IsDownloading ? Strings.Format("Update_Downloading", DownloadProgress.ToString("P0", CultureInfo.CurrentCulture))
        : u.Asset is null ? Strings.Get("Update_NoAsset")
        : Strings.Get(Kind == InstallKind.Installer ? "Update_InstallerHint" : "Update_PortableHint");

    /// <summary>Starts the daily schedule.</summary>
    public void Start() => Schedule();

    partial void OnCheckAutomaticallyChanged(bool value)
    {
        _settings.Update(s => s with { Updates = s.Updates with { CheckAutomatically = value } });
        Schedule();
    }

    private void Schedule()
    {
        _timer.Stop();
        if (!CheckAutomatically)
            return;
        var due = (_settings.Current.Updates.LastCheck ?? DateTimeOffset.MinValue) + CheckInterval;
        var wait = due - DateTimeOffset.Now;
        _timer.Interval = wait > StartupDelay ? wait : StartupDelay;
        _timer.Start();
        LogScheduled(_timer.Interval);
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckNowAsync() => CheckAsync(userInitiated: true);

    private bool CanCheck() => !IsChecking && !IsDownloading;

    private async Task CheckAsync(bool userInitiated)
    {
        IsChecking = true;
        Status = Strings.Get("Update_Checking");
        try
        {
            var update = await _updater.CheckAsync(Kind);
            _settings.Update(s => s with { Updates = s.Updates with { LastCheck = DateTimeOffset.Now } });

            if (update is null)
            {
                Available = null;
                BannerOpen = false;
                Status = Strings.Format("Update_UpToDate", LastCheckText());
                if (userInitiated)
                    _notifications.Show(Strings.Get("Notice_Updates_Title"), Strings.Format("Update_Latest", CurrentVersionText), InfoBarSeverity.Success);
                return;
            }

            // Automatic checks respect "skip this version"; asking explicitly always shows it.
            if (!userInitiated && _settings.Current.Updates.SkippedVersion == update.Tag)
            {
                Status = Strings.Format("Update_Skipped", update.Version.ToString(3), LastCheckText());
                return;
            }

            Available = update;
            BannerOpen = true;
            Status = Strings.Format("Update_Available", update.Version.ToString(3), LastCheckText());
            LogAvailable(update.Tag);
            if (!userInitiated && !App.Current.IsWindowVisible)
                _notifications.Alert(BannerTitle, Strings.Get("Update_OpenToUpdate"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogCheckFailed(ex);
            Status = Strings.Format("Update_CheckFailed", ex.Message);
            if (userInitiated)
                _notifications.Show(Strings.Get("Notice_Updates_Title"), Status, InfoBarSeverity.Error);
        }
        finally
        {
            IsChecking = false;
            Schedule();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (Available is not { } update)
            return;
        if (update.Asset is not { } asset)
        {
            OpenReleasePage();
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        try
        {
            var path = Path.Combine(UpdateInstaller.DownloadDirectory, asset.Name);
            await _updater.DownloadAsync(asset, path, new Progress<double>(p => DownloadProgress = p));

            if (Kind == InstallKind.Installer)
            {
                if (!UpdateInstaller.StartSetup(path))
                {
                    _notifications.Show(Strings.Get("Notice_Updates_Title"), Strings.Get("Update_Cancelled"), InfoBarSeverity.Informational);
                    return;
                }
            }
            else
            {
                UpdateInstaller.StartPortableUpdate(path);
            }

            // Setup (or the new portable copy) takes it from here and reopens OpenSense.
            LogInstalling(update.Tag, Kind);
            App.Current.Quit();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException
                                       or UpdateVerificationException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            LogInstallFailed(ex);
            var reason = ex is UpdateVerificationException verification
                ? Strings.Get(verification.Failure == VerificationFailure.NoDigest ? "Update_NoDigest" : "Update_DigestMismatch")
                : ex.Message;
            _notifications.Show(Strings.Get("Notice_Updates_Title"), Strings.Format("Update_Failed", reason), InfoBarSeverity.Error);
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private bool CanInstall() => Available is not null && !IsDownloading;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private void Skip()
    {
        if (Available is not { } update)
            return;
        _settings.Update(s => s with { Updates = s.Updates with { SkippedVersion = update.Tag } });
        BannerOpen = false;
        Status = Strings.Format("Update_Skipped", update.Version.ToString(3), LastCheckText());
    }

    [RelayCommand(CanExecute = nameof(HasUpdate))]
    private void OpenReleasePage()
    {
        if (Available is { } update)
            Process.Start(new ProcessStartInfo(update.ReleasePage.ToString()) { UseShellExecute = true })?.Dispose();
    }

    private string LastCheckText() => _settings.Current.Updates.LastCheck is { } last
        ? Strings.Format("Update_LastChecked", last.LocalDateTime.ToString("g", CultureInfo.CurrentCulture))
        : Strings.Get("Update_NotChecked");

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next update check in {Wait}")]
    private partial void LogScheduled(TimeSpan wait);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update available: {Tag}")]
    private partial void LogAvailable(string tag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed")]
    private partial void LogCheckFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {Tag} ({Kind})")]
    private partial void LogInstalling(string tag, InstallKind kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Update failed")]
    private partial void LogInstallFailed(Exception ex);
}
