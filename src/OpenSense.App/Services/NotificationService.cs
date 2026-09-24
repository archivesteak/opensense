using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace OpenSense.App.Services;

public sealed record Notice(string Title, string Message, InfoBarSeverity Severity);

/// <summary>In-app notices (shown as InfoBars) raised from any thread.</summary>
public sealed class NotificationService(DispatcherQueue dispatcher)
{
    private const int MaxNotices = 3;

    public ObservableCollection<Notice> Notices { get; } = [];

    /// <summary>Raised on the UI thread for notices worth a tray balloon when the window is hidden.</summary>
    public event Action<Notice>? Important;

    public void Show(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Warning, bool important = false)
    {
        dispatcher.TryEnqueue(() =>
        {
            var notice = new Notice(title, message, severity);
            if (Notices.Any(n => n.Message == message))
                return;
            Notices.Insert(0, notice);
            while (Notices.Count > MaxNotices)
                Notices.RemoveAt(Notices.Count - 1);
            if (important)
                Important?.Invoke(notice);
        });
    }

    public void Dismiss(Notice notice) => Notices.Remove(notice);

    /// <summary>Removes the notice showing <paramref name="message"/>, once what it reported is over.</summary>
    public void Dismiss(string message) =>
        dispatcher.TryEnqueue(() =>
        {
            if (Notices.FirstOrDefault(n => n.Message == message) is { } notice)
                Notices.Remove(notice);
        });

    /// <summary>A notification-area balloon only, for news shown elsewhere in the window (e.g. the update banner).</summary>
    public void Alert(string title, string message) =>
        dispatcher.TryEnqueue(() => Important?.Invoke(new Notice(title, message, InfoBarSeverity.Informational)));
}
