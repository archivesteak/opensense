using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OpenSense.App.Localization;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;
using OpenSense.Core.Settings;

namespace OpenSense.App.Services;

public enum SessionState
{
    Starting,
    Ready,

    /// <summary>Portable copy (no service) without administrator rights: the user declined the UAC prompt.</summary>
    NeedsElevation,

    /// <summary>The OpenSense service is installed but not running.</summary>
    ServiceUnavailable,

    /// <summary>No Acer gaming firmware on this machine.</summary>
    Unsupported,

    Failed,
}

public enum SessionChange
{
    /// <summary>Capability overrides changed and the service rebuilt the session.</summary>
    Rebuilt,

    /// <summary>The service restarted and the app reconnected.</summary>
    Reconnected,

    /// <summary>Another client (another Windows user's OpenSense) changed the settings.</summary>
    SettingsChanged,
}

/// <summary>
/// The app's link to the engine: the OpenSense service over its pipe when OpenSense is installed, or an
/// engine hosted in this process for a portable copy. Events arrive on worker threads.
/// </summary>
public sealed partial class DeviceSession : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartupWait = TimeSpan.FromSeconds(30);

    /// <summary>Settings echoes shortly after a local change are ours; remote ones are reconciled after this.</summary>
    private static readonly TimeSpan LocalChangeSettle = TimeSpan.FromSeconds(2);

    private readonly ILoggerFactory _loggers;
    private readonly ILogger<DeviceSession> _log;
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();
    private readonly Timer _reconcileTimer;

    private IOpenSenseService? _service;
    private OpenSenseConnection? _connection;
    private OpenSenseEngine? _engine;
    private EngineSnapshot _snapshot = new();
    private DateTime _lastLocalChange = DateTime.MinValue;

    public DeviceSession(ILoggerFactory loggers)
    {
        _loggers = loggers;
        _log = loggers.CreateLogger<DeviceSession>();
        _reconcileTimer = new Timer(_ => _ = ReconcileAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public SessionState State { get; private set; } = SessionState.Starting;

    public string? Error { get; private set; }

    /// <summary>No service: this process owns the firmware (portable copy).</summary>
    public bool IsPortable { get; private set; }

    /// <summary>Connected to the OpenSense service.</summary>
    public bool UsesService => _connection is not null;

    /// <summary>The laptop's model name, if the firmware reports one.</summary>
    public string? DeviceName => _snapshot.DeviceName;

    public string? BiosVersion => _snapshot.BiosVersion;

    public string? SerialNumber => _snapshot.SerialNumber;

    public string? CpuName => _snapshot.CpuName;

    public string? GpuName => _snapshot.GpuName;

    /// <summary>What detection found, before user overrides.</summary>
    public DeviceCapabilities Detected => _snapshot.Detected;

    /// <summary>What is in use (detection + overrides).</summary>
    public DeviceCapabilities Capabilities => _snapshot.Capabilities;

    /// <summary>The firmware's state when the engine started (the GPU mode follows later switches).</summary>
    public FirmwareState? Firmware => _snapshot.Firmware;

    /// <summary>The keyboard's state when the engine started.</summary>
    public KeyboardState? InitialKeyboard => _snapshot.Keyboard;

    /// <summary>What the lights showed when the engine started, by light.</summary>
    public IReadOnlyDictionary<string, LightingSettings>? InitialLighting => _snapshot.Lighting;

    public TemperatureSources TemperatureSources => _snapshot.TemperatureSources;

    /// <summary>The machine settings, including changes this app just made.</summary>
    public MachineSettings Settings
    {
        get
        {
            lock (_gate)
                return _snapshot.Settings;
        }
    }

    public Telemetry? Latest { get; private set; }

    public event Action<Telemetry>? TelemetryUpdated;

    public event Action<ControlNotice>? Notice;

    /// <summary>The snapshot was replaced; views should reload from it.</summary>
    public event Action<SessionChange>? Changed;

    /// <summary>The connection to the service dropped (false) or came back (true).</summary>
    public event Action<bool>? ConnectionChanged;

    public async Task InitializeAsync()
    {
        State = SessionState.Starting;
        Error = null;
        try
        {
            if (OpenSensePipe.IsServiceInstalled)
            {
                if (!await ConnectWhileServiceStartsAsync().ConfigureAwait(false))
                {
                    State = SessionState.ServiceUnavailable;
                    return;
                }
            }
            else if (SystemInfo.IsElevated)
            {
                // Portable copy: no service, so this (elevated) process owns the firmware while it runs.
                SettingsPaths.SecureMachineDirectory();
                HostEngine();
                IsPortable = true;
            }
            else
            {
                State = SessionState.NeedsElevation; // Program offered elevation at startup and it was declined
                return;
            }

            var snapshot = await WaitUntilStartedAsync().ConfigureAwait(false);
            Apply(snapshot);
            State = snapshot.State switch
            {
                EngineState.Ready => SessionState.Ready,
                EngineState.Unsupported => SessionState.Unsupported,
                _ => SessionState.Failed,
            };
            Error = snapshot.Error;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogStartFailed(ex);
            Error = ex.Message;
            State = SessionState.Failed;
        }
    }

    public Task SetProfileAsync(ControlProfile profile) =>
        ChangeAsync(s => s with { Profile = profile }, service => service.SetProfileAsync(profile));

    public Task SetKeyboardAsync(KeyboardSettings keyboard) =>
        ChangeAsync(s => s with { Keyboard = keyboard }, service => service.SetKeyboardAsync(keyboard));

    public Task SetLightingAsync(LightingConfig lighting) =>
        ChangeAsync(s => s with { Lighting = lighting }, service => service.SetLightingAsync(lighting));

    public Task SetPowerAsync(PowerSettings power) =>
        ChangeAsync(s => s with { Power = power }, service => service.SetPowerAsync(power));

    public async Task<CalibrationResult> StartBatteryCalibrationAsync()
    {
        if (_service is not { } service)
            return CalibrationResult.Rejected;
        try
        {
            return await service.StartBatteryCalibrationAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCallFailure(ex))
        {
            LogCallFailed(ex);
            return CalibrationResult.Rejected;
        }
    }

    public async Task StopBatteryCalibrationAsync()
    {
        if (_service is not { } service)
            return;
        try
        {
            await service.StopBatteryCalibrationAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCallFailure(ex))
        {
            LogCallFailed(ex);
        }
    }

    /// <summary>How worn the battery is; null without one, or when the engine can't be reached.</summary>
    public Task<BatteryHealth?> ReadBatteryHealthAsync() => CallAsync(service => service.ReadBatteryHealthAsync(), null);

    /// <summary>Rebuilds the session with new overrides; <see cref="Changed"/> follows with <see cref="SessionChange.Rebuilt"/>.</summary>
    public Task SetOverridesAsync(CapabilityOverrides overrides) =>
        ChangeAsync(s => s with { Overrides = overrides }, service => service.SetOverridesAsync(overrides));

    public async Task<bool> SetGpuModeAsync(GpuMode mode)
    {
        if (_service is not { } service)
            return false;
        try
        {
            var ok = await service.SetGpuModeAsync(mode).ConfigureAwait(false);
            if (ok)
            {
                lock (_gate)
                    _snapshot = _snapshot with { Firmware = _snapshot.Firmware is { } f ? f with { GpuMode = mode } : null };
            }
            return ok;
        }
        catch (Exception ex) when (IsCallFailure(ex))
        {
            LogCallFailed(ex);
            return false;
        }
    }

    public async Task<bool> SetBootAnimationAsync(bool enabled)
    {
        var ok = await CallAsync(service => service.SetBootAnimationAsync(enabled), false).ConfigureAwait(false);
        if (ok)
        {
            lock (_gate)
                _snapshot = _snapshot with { Firmware = _snapshot.Firmware is { } f ? f with { BootAnimation = enabled } : null };
        }
        return ok;
    }

    public Task<BootLogoState> GetBootLogoAsync() => CallAsync(service => service.GetBootLogoAsync(), BootLogoState.None);

    public Task<BootLogoResult> SetBootLogoAsync(byte[] image) =>
        CallAsync(service => service.SetBootLogoAsync(image), BootLogoResult.WriteFailed);

    public Task<bool> RestoreBootLogoAsync() => CallAsync(service => service.RestoreBootLogoAsync(), false);

    public Task<DustDefenderStart> StartDustDefenderAsync() =>
        CallAsync(service => service.StartDustDefenderAsync(), DustDefenderStart.Failed);

    /// <summary>Asks the engine; <paramref name="fallback"/> when it can't be reached.</summary>
    private async Task<T> CallAsync<T>(Func<IOpenSenseService, Task<T>> call, T fallback)
    {
        if (_service is not { } service)
            return fallback;
        try
        {
            return await call(service).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCallFailure(ex))
        {
            LogCallFailed(ex);
            return fallback;
        }
    }

    /// <summary>The engine's diagnostics (probe answers, recent firmware events); what detection found if it can't be asked.</summary>
    public async Task<string> GetDiagnosticsAsync()
    {
        if (_service is { } service)
        {
            try
            {
                return await service.GetDiagnosticsAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsCallFailure(ex))
            {
                LogCallFailed(ex);
            }
        }
        return Detected.Diagnostics;
    }

    private async Task ChangeAsync(Func<MachineSettings, MachineSettings> change, Func<IOpenSenseService, Task> send)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with { Settings = change(_snapshot.Settings) };
            _lastLocalChange = DateTime.UtcNow;
        }
        if (_service is not { } service)
            return;
        try
        {
            await send(service).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCallFailure(ex))
        {
            // Reconnecting; the view keeps the change and it is re-sent with the next one.
            LogCallFailed(ex);
        }
    }

    private void HostEngine()
    {
        var engine = new OpenSenseEngine(new WindowsMachine(), SettingsPaths.Machine, _loggers.CreateLogger<OpenSenseEngine>());
        engine.Start();
        _engine = engine;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Attach(engine);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
            _engine?.NotifySuspend();
        else if (e.Mode == PowerModes.Resume)
            _engine?.NotifyResume();
        else if (e.Mode == PowerModes.StatusChange)
            _engine?.NotifyPowerSourceChanged();
    }

    /// <summary>Right after boot the service may still be starting; wait for it rather than report it stopped.</summary>
    private async Task<bool> ConnectWhileServiceStartsAsync()
    {
        var deadline = DateTime.UtcNow + StartupWait;
        while (true)
        {
            if (await ConnectAsync().ConfigureAwait(false))
                return true;
            if (!ServiceIsRunningOrStarting() || DateTime.UtcNow > deadline)
                return false;
        }
    }

    private static bool ServiceIsRunningOrStarting()
    {
        using var controller = new ServiceController(OpenSensePipe.ServiceName);
        try
        {
            return controller.Status is ServiceControllerStatus.StartPending or ServiceControllerStatus.Running;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<bool> ConnectAsync()
    {
        var connection = await OpenSensePipe.ConnectAsync(ConnectTimeout, _stopping.Token).ConfigureAwait(false);
        if (connection is null)
            return false;
        _connection = connection;
        Attach(connection.Service);
        _ = WatchConnectionAsync(connection);
        return true;
    }

    private void Attach(IOpenSenseService service)
    {
        _service = service;
        service.TelemetryUpdated += OnTelemetry;
        service.NoticeRaised += OnNotice;
        service.Rebuilt += OnRebuilt;
        service.SettingsChanged += OnSettingsChanged;
    }

    private void OnTelemetry(object? sender, Telemetry telemetry)
    {
        Latest = telemetry;
        TelemetryUpdated?.Invoke(telemetry);
    }

    private void OnNotice(object? sender, ControlNotice notice) => Notice?.Invoke(notice);

    private void OnRebuilt(object? sender, EngineSnapshot snapshot)
    {
        Apply(snapshot);
        Changed?.Invoke(SessionChange.Rebuilt);
    }

    private void OnSettingsChanged(object? sender, MachineSettings settings)
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _lastLocalChange < LocalChangeSettle)
            {
                // Most likely the echo of our own change; check again once the user's edits settle.
                _reconcileTimer.Change(LocalChangeSettle, Timeout.InfiniteTimeSpan);
                return;
            }
            if (SameSettings(settings, _snapshot.Settings))
                return;
            _snapshot = _snapshot with { Settings = settings };
        }
        Changed?.Invoke(SessionChange.SettingsChanged);
    }

    private async Task ReconcileAsync()
    {
        if (_service is not { } service || DateTime.UtcNow - _lastLocalChange < LocalChangeSettle)
            return;
        try
        {
            var remote = (await service.GetSnapshotAsync().ConfigureAwait(false)).Settings;
            lock (_gate)
            {
                if (SameSettings(remote, _snapshot.Settings))
                    return;
                _snapshot = _snapshot with { Settings = remote };
            }
            Changed?.Invoke(SessionChange.SettingsChanged);
        }
        catch (Exception ex) when (IsCallFailure(ex))
        {
            LogCallFailed(ex);
        }
    }

    private static bool SameSettings(MachineSettings a, MachineSettings b) =>
        JsonSerializer.Serialize(a, SettingsJson.Options) == JsonSerializer.Serialize(b, SettingsJson.Options);

    private void Apply(EngineSnapshot snapshot)
    {
        if (snapshot.ProtocolVersion != OpenSensePipe.ProtocolVersion)
        {
            throw new InvalidOperationException(Strings.Format("Session_VersionMismatch", snapshot.EngineVersion));
        }
        lock (_gate)
            _snapshot = snapshot;
        Latest = snapshot.Latest;
    }

    private async Task<EngineSnapshot> WaitUntilStartedAsync()
    {
        var deadline = DateTime.UtcNow + StartupWait;
        while (true)
        {
            var snapshot = await _service!.GetSnapshotAsync(_stopping.Token).ConfigureAwait(false);
            if (snapshot.State != EngineState.Starting || DateTime.UtcNow > deadline)
                return snapshot;
            await Task.Delay(500, _stopping.Token).ConfigureAwait(false);
        }
    }

    /// <summary>When the service stops (update, crash, restart), keep trying to reconnect.</summary>
    private async Task WatchConnectionAsync(OpenSenseConnection connection)
    {
        try
        {
            await connection.Completion.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogConnectionFailed(ex);
        }
        if (_stopping.IsCancellationRequested)
            return;

        LogDisconnected();
        _service = null;
        _connection = null;
        connection.Dispose();
        ConnectionChanged?.Invoke(false);

        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RetryDelay, _stopping.Token).ConfigureAwait(false);
                if (!await ConnectAsync().ConfigureAwait(false))
                    continue;
                Apply(await WaitUntilStartedAsync().ConfigureAwait(false));
                LogReconnected();
                ConnectionChanged?.Invoke(true);
                Changed?.Invoke(SessionChange.Reconnected);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogConnectionFailed(ex);
            }
        }
    }

    /// <summary>The service went away mid-call, or refused the call; logged, never fatal to the UI.</summary>
    private static bool IsCallFailure(Exception ex) =>
        ex is StreamJsonRpc.ConnectionLostException or ObjectDisposedException or IOException or StreamJsonRpc.RemoteInvocationException;

    public void Dispose()
    {
        _stopping.Cancel();
        _reconcileTimer.Dispose();
        _connection?.Dispose(); // the service keeps controlling the fans
        if (_engine is { } engine)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            engine.Dispose(); // this process owned the fans: hand them back if configured
        }
        _stopping.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start the device session")]
    private partial void LogStartFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Call to the OpenSense service failed")]
    private partial void LogCallFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lost the connection to the OpenSense service")]
    private partial void LogDisconnected();

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnected to the OpenSense service")]
    private partial void LogReconnected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenSense service connection error")]
    private partial void LogConnectionFailed(Exception ex);
}
