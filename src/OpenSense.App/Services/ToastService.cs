using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using OpenSense.Core.Updates;

namespace OpenSense.App.Services;

/// <summary>
/// Windows notifications (toasts): the <see cref="NotificationService.Important"/> notices and <see cref="Show"/>.
/// <list type="bullet">
/// <item>The installed app registers with Windows App SDK app notifications as "OpenSense": a toast can replace the one
/// before it (same tag), and clicking one opens the window. The registration is per user and kept between runs, so
/// toasts left in the notification centre still open the app; the uninstaller removes it.</item>
/// <item>A portable copy has no uninstaller to remove a registration, and Windows gives no app notifications to an
/// elevated process (a portable copy owning the laptop, or the app run as administrator). These use the
/// notification-area icon's balloon, which Windows also shows as a toast.</item>
/// </list>
/// </summary>
public sealed partial class ToastService(DispatcherQueue dispatcher, TrayService tray, NotificationService notifications,
    ILogger<ToastService> log) : IDisposable
{
    private const string DisplayName = "OpenSense";
    private const string Group = "OpenSense";

    private bool _registered;

    /// <summary>Registers for app notifications where Windows allows them; call on the UI thread, after the tray icon exists.</summary>
    public void Initialize()
    {
        notifications.Important += notice =>
            Show(notice.Title, notice.Message, warning: notice.Severity is InfoBarSeverity.Warning or InfoBarSeverity.Error);

        if (UpdateInstaller.DetectKind() != InstallKind.Installer || !AppNotificationManager.IsSupported())
            return;
        var manager = AppNotificationManager.Default;
        // Before Register, so that a click reaches this process instead of starting another one.
        manager.NotificationInvoked += (_, _) => dispatcher.TryEnqueue(App.Current.ShowMainWindow);
        try
        {
            manager.Register(DisplayName, new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "OpenSense.png")));
            _registered = true;
        }
        catch (Exception ex) when (ex is COMException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            LogRegisterFailed(ex);
        }
    }

    /// <summary>
    /// Shows a toast; call on the UI thread. One with the same <paramref name="tag"/> replaces the one before, and
    /// <paramref name="expireAfter"/> takes it out of the notification centre after that long.
    /// </summary>
    public void Show(string title, string message, string? tag = null, TimeSpan? expireAfter = null, bool warning = false)
    {
        if (App.Current.IsExiting)
            return;

        if (_registered)
        {
            var toast = new AppNotificationBuilder().AddText(title).AddText(message).BuildNotification();
            if (tag is not null)
            {
                toast.Tag = tag;
                toast.Group = Group;
            }
            if (expireAfter is { } after)
                toast.Expiration = DateTimeOffset.Now + after;
            try
            {
                AppNotificationManager.Default.Show(toast);
                return;
            }
            catch (COMException ex)
            {
                LogCallFailed(ex);
            }
        }
        tray.ShowBalloon(title, message, warning);
    }

    public void Dispose()
    {
        if (!_registered)
            return;
        _registered = false;
        try
        {
            // Hands clicks back to Windows, which starts the app for them; the registration itself stays.
            AppNotificationManager.Default.Unregister();
        }
        catch (COMException ex)
        {
            LogCallFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "App notifications unavailable; using the notification-area icon")]
    private partial void LogRegisterFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "App notification call failed")]
    private partial void LogCallFailed(Exception ex);
}
