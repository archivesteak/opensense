using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using OpenSense.App.Localization;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.App.Services;

/// <summary>
/// Watches Windows power-source events, resume/unlock transitions, and device telemetry in the interactive
/// desktop session so battery lowers displays to 60 Hz and AC power restores their highest refresh rate.
/// </summary>
public sealed partial class DisplayRefreshService(
    DisplayRefreshController controller,
    SettingsService settings,
    DeviceSession session,
    DispatcherQueue dispatcher,
    ToastService toasts,
    ILogger<DisplayRefreshService> log) : IDisposable
{
    private const string RefreshRateToastTag = "refreshrate";
    private static readonly TimeSpan RefreshRateToastLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ResumeRecheckDelay = TimeSpan.FromMilliseconds(1500);

    private readonly object _gate = new();

    private Timer? _resumeTimer;
    private bool? _pendingEnabled;
    private bool _pendingReconcile;
    private bool _workerRunning;
    private bool _started;
    private bool _disposed;

    public bool IsSwitchAvailable => controller.IsSwitchAvailable;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _started)
                return;
            _started = true;
            _resumeTimer = new Timer(_ => EnqueueReconcile(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        session.TelemetryUpdated += OnTelemetry;

        SetEnabled(settings.Current.Ui.RefreshRateOnBattery);
    }

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _pendingEnabled = enabled;
            StartWorkerLocked();
        }
    }

    private void OnTelemetry(Telemetry telemetry)
    {
        if (_disposed || !controller.NeedsReconcile(telemetry.OnAcPower))
            return;
        EnqueueReconcile();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (_disposed)
            return;

        if (e.Mode is PowerModes.StatusChange)
        {
            EnqueueReconcile();
        }
        else if (e.Mode is PowerModes.Resume && controller.OnPowerResume())
        {
            EnqueueReconcile();
            lock (_gate)
            {
                if (!_disposed)
                    _resumeTimer?.Change(ResumeRecheckDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (_disposed || e.Reason is not SessionSwitchReason.SessionUnlock)
            return;
        if (controller.OnDesktopUnlocked())
            EnqueueReconcile();
    }

    private void EnqueueReconcile()
    {
        lock (_gate)
        {
            if (_disposed || !_started)
                return;
            if (!controller.IsEnabled && _pendingEnabled != true)
                return;
            _pendingReconcile = true;
            StartWorkerLocked();
        }
    }

    private void StartWorkerLocked()
    {
        if (_workerRunning || _disposed)
            return;
        _workerRunning = true;
        _ = Task.Run(RunWorker);
    }

    private void RunWorker()
    {
        while (true)
        {
            bool? enable;
            bool reconcile;
            lock (_gate)
            {
                if (_disposed || (_pendingEnabled is null && !_pendingReconcile))
                {
                    _workerRunning = false;
                    return;
                }

                enable = _pendingEnabled;
                reconcile = _pendingReconcile;
                _pendingEnabled = null;
                _pendingReconcile = false;
            }

            try
            {
                if (enable is { } enabled)
                {
                    var enableResult = controller.SetEnabled(enabled);
                    LogConfigured(enabled);
                    HandleResult(enableResult);
                }
                // Cheap no-op when SetEnabled just applied; never drops a reconcile queued alongside a setting change.
                if (reconcile)
                    HandleResult(controller.Reconcile());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogReconcileError(ex);
            }
        }
    }

    private void HandleResult(DisplayRefreshResult result)
    {
        if (result.Outcome == DisplayRefreshOutcome.Failed)
        {
            LogReconcileFailed();
            return;
        }

        if (result.Outcome != DisplayRefreshOutcome.Applied || result.SwitchedHz is not { } hz)
            return;

        var title = Strings.Get("Notice_Display_Title");
        var message = result.OnAc
            ? Strings.Format("Notice_RefreshRatePluggedIn", hz)
            : Strings.Format("Notice_RefreshRateOnBattery", hz);
        dispatcher.TryEnqueue(() => toasts.Show(title, message, RefreshRateToastTag, RefreshRateToastLifetime));
    }

    public void Dispose()
    {
        Timer? timer;
        bool wasStarted;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            wasStarted = _started;
            timer = _resumeTimer;
            _resumeTimer = null;
        }

        if (wasStarted)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            session.TelemetryUpdated -= OnTelemetry;
        }
        timer?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Automatic 60 Hz on battery set to {Enabled}")]
    private partial void LogConfigured(bool enabled);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not switch display refresh rate for the current power source")]
    private partial void LogReconcileFailed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Display refresh rate check failed")]
    private partial void LogReconcileError(Exception ex);
}
