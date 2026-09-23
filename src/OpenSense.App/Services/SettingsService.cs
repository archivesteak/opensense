using Microsoft.Extensions.Logging;
using OpenSense.Core.Settings;

namespace OpenSense.App.Services;

/// <summary>
/// Holds this user's <see cref="UserSettings"/> (how the app looks and behaves) and saves changes shortly
/// after they happen. What the laptop does lives in the service: see <see cref="DeviceSession.Settings"/>.
/// </summary>
public sealed partial class SettingsService : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly SettingsStore<UserSettings> _store;
    private readonly ILogger<SettingsService> _log;
    private readonly object _gate = new();
    private readonly Timer _saveTimer;

    public SettingsService(SettingsStore<UserSettings> store, ILogger<SettingsService> log)
    {
        _store = store;
        _log = log;
        Current = store.Load();
        _saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public UserSettings Current { get; private set; }

    public string FilePath => _store.Path;

    /// <summary>Raised after every change, on the thread that made it.</summary>
    public event Action<UserSettings>? Changed;

    public void Update(Func<UserSettings, UserSettings> change)
    {
        UserSettings updated;
        lock (_gate)
        {
            updated = change(Current);
            if (updated == Current)
                return;
            Current = updated;
            _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke(updated);
    }

    public void Flush()
    {
        UserSettings snapshot;
        lock (_gate)
            snapshot = Current;
        try
        {
            _store.Save(snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(ex, _store.Path);
        }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        Flush();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not save settings to {Path}")]
    private partial void LogSaveFailed(Exception ex, string path);
}
