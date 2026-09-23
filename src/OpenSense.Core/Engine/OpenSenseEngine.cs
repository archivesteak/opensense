using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Engine;

/// <summary>
/// Everything that talks to the laptop: transport, capability detection, temperature sensors, the fan
/// control loop, the keyboard and the machine settings. Hosted by the OpenSense service, or by the app when
/// it runs as a portable copy; clients use it through <see cref="IOpenSenseService"/>.
/// </summary>
public sealed partial class OpenSenseEngine : IOpenSenseService, IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);
    private static readonly string Version =
        typeof(OpenSenseEngine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    private readonly IMachine _machine;
    private readonly ILogger<OpenSenseEngine> _log;
    private readonly SettingsStore<MachineSettings> _store;
    private readonly Timer _saveTimer;
    private readonly object _settingsGate = new();
    private readonly SemaphoreSlim _sessionGate = new(1, 1);

    private MachineSettings _settings;
    private IWmiTransport? _transport;
    private ILoadMonitor? _load;
    private DirectSensors _sensors = DirectSensors.None;
    private FanControlService? _controller;
    private KeyboardService? _keyboard;

    private EngineState _state = EngineState.Starting;
    private string? _error;
    private string? _deviceName;
    private string? _biosVersion;
    private DeviceCapabilities _detected = DeviceCapabilities.None;
    private DeviceCapabilities _capabilities = DeviceCapabilities.None;
    private FirmwareState? _firmware;
    private KeyboardState? _keyboardAtStart;
    private int _disposed;

    /// <param name="settingsPath">Where the machine settings live (<see cref="SettingsPaths.Machine"/>).</param>
    public OpenSenseEngine(IMachine machine, string settingsPath, ILogger<OpenSenseEngine> log)
    {
        _machine = machine;
        _log = log;
        _store = new SettingsStore<MachineSettings>(settingsPath);
        _settings = _store.Load();
        _saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler<Telemetry>? TelemetryUpdated;

    public event EventHandler<ControlNotice>? NoticeRaised;

    public event EventHandler<EngineSnapshot>? Rebuilt;

    public event EventHandler<MachineSettings>? SettingsChanged;

    public EngineState State => _state;

    /// <summary>Detects the laptop and starts controlling it. Never throws; see <see cref="State"/>.</summary>
    public void Start()
    {
        _sessionGate.Wait();
        try
        {
            _transport = _machine.OpenFirmware();
            var device = new AcerDevice(_transport);
            if (!device.IsPresent)
            {
                _state = EngineState.Unsupported;
                LogUnsupported();
                return;
            }

            _deviceName = _machine.Model;
            _biosVersion = _machine.BiosVersion;
            _detected = CapabilityProbe.Probe(device, _machine.ReadHints(), _machine.ReadSmbios());
            LogDetected(_deviceName ?? "Unknown model", _detected.Diagnostics);
            _capabilities = Current.Overrides.Apply(_detected);

            _firmware = FirmwareState.Read(device, _capabilities);
            _keyboardAtStart = KeyboardState.Read(device, _capabilities.Keyboard);
            _load = _machine.OpenLoadMonitor();
            _sensors = _machine.OpenSensors();
            LogSensors(_sensors.CpuStatus, _sensors.GpuStatus);

            // First start on this machine: keep whatever NitroSense (or the firmware) is set to.
            if (!_store.Exists)
                AdoptFirmwareState();

            StartControl(device);
            _state = EngineState.Ready;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogStartFailed(ex);
            _error = ex.Message;
            _state = EngineState.Failed;
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    /// <summary>The machine woke from sleep. (Services do not get SystemEvents; the host forwards power events.)</summary>
    public void NotifyResume()
    {
        _controller?.OnResume();
        _keyboard?.OnResume();
    }

    /// <summary>AC power was connected or removed.</summary>
    public void NotifyPowerSourceChanged() => _controller?.OnPowerSourceChanged();

    public async Task<EngineSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Snapshot();
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public Task SetProfileAsync(ControlProfile profile, CancellationToken cancellationToken = default)
    {
        UpdateSettings(s => s with { Profile = profile });
        _controller?.Update(profile);
        return Task.CompletedTask;
    }

    public Task SetKeyboardAsync(KeyboardSettings keyboard, CancellationToken cancellationToken = default)
    {
        UpdateSettings(s => s with { Keyboard = keyboard });
        return _keyboard?.ApplyAsync(keyboard) ?? Task.CompletedTask;
    }

    public async Task SetOverridesAsync(CapabilityOverrides capabilityOverrides, CancellationToken cancellationToken = default)
    {
        UpdateSettings(s => s with { Overrides = capabilityOverrides });
        EngineSnapshot rebuilt;
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state != EngineState.Ready || _transport is null)
                return;
            // Restarting the loop hands the fans back to Auto briefly (if configured), then re-applies the profile.
            await Task.Run(() =>
            {
                StopControl();
                _capabilities = capabilityOverrides.Apply(_detected);
                StartControl(new AcerDevice(_transport));
            }, cancellationToken).ConfigureAwait(false);
            rebuilt = Snapshot();
        }
        finally
        {
            _sessionGate.Release();
        }
        LogRebuilt();
        Rebuilt?.Invoke(this, rebuilt);
    }

    public Task SetPollIntervalAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        var interval = Math.Clamp(milliseconds, 250, 5000);
        UpdateSettings(s => s with { PollIntervalMs = interval });
        if (_controller is { } controller)
            controller.Interval = TimeSpan.FromMilliseconds(interval);
        return Task.CompletedTask;
    }

    public async Task<bool> SetGpuModeAsync(GpuMode mode, CancellationToken cancellationToken = default)
    {
        if (_controller is not { } controller || !_capabilities.GpuModeSwitch)
            return false;
        var ok = await controller.InvokeAsync(d => d.SetGpuMode(mode)).ConfigureAwait(false);
        if (ok && _firmware is { } firmware)
            _firmware = firmware with { GpuMode = mode };
        LogGpuMode(mode, ok);
        return ok;
    }

    public async Task<KeyboardState?> ReadKeyboardAsync(CancellationToken cancellationToken = default) =>
        _keyboard is { } keyboard ? await keyboard.ReadStateAsync().ConfigureAwait(false) : null;

    private MachineSettings Current
    {
        get
        {
            lock (_settingsGate)
                return _settings;
        }
    }

    private EngineSnapshot Snapshot() => new()
    {
        EngineVersion = Version,
        State = _state,
        Error = _error,
        DeviceName = _deviceName,
        BiosVersion = _biosVersion,
        CpuName = _load?.CpuName,
        GpuName = _load?.GpuName,
        Detected = _detected,
        Capabilities = _capabilities,
        Firmware = _firmware,
        Keyboard = _keyboardAtStart,
        Settings = Current,
        TemperatureSources = new TemperatureSources(_sensors.CpuStatus, _sensors.GpuStatus),
        Latest = _controller?.Latest,
    };

    private void StartControl(AcerDevice device)
    {
        var settings = Current;
        _controller = new FanControlService(device, _capabilities, _load!, new SystemPowerSource(), settings.Profile, _sensors)
        {
            Interval = TimeSpan.FromMilliseconds(Math.Clamp(settings.PollIntervalMs, 250, 5000)),
        };
        _controller.TelemetryUpdated += OnTelemetry;
        _controller.Notice += OnNotice;
        _controller.Start();

        _keyboard = new KeyboardService(_controller, _capabilities.Keyboard);
        _keyboard.Notice += OnNotice;
        _ = _keyboard.ApplyAsync(settings.Keyboard);
    }

    private void StopControl()
    {
        if (_keyboard is { } keyboard)
            keyboard.Notice -= OnNotice;
        _keyboard = null;
        if (_controller is { } controller)
        {
            controller.TelemetryUpdated -= OnTelemetry;
            controller.Notice -= OnNotice;
            controller.Dispose(); // hands the fans back to the firmware if configured
        }
        _controller = null;
    }

    private void OnTelemetry(Telemetry telemetry) => TelemetryUpdated?.Invoke(this, telemetry);

    private void OnNotice(ControlNotice notice)
    {
        LogNotice(notice.Kind, notice.OperatingMode, notice.Detail);
        NoticeRaised?.Invoke(this, notice);
    }

    private void AdoptFirmwareState()
    {
        var firmware = _firmware!;
        UpdateSettings(s => s with
        {
            Profile = firmware.ToProfile(s.Profile),
            Keyboard = s.Keyboard with
            {
                BacklightAutoOff = _keyboardAtStart?.BacklightAutoOff,
                WindowsKey = _keyboardAtStart?.WindowsKey,
                LcdOverdrive = _keyboardAtStart?.LcdOverdrive,
            },
        });
        Flush();
    }

    private void UpdateSettings(Func<MachineSettings, MachineSettings> change)
    {
        MachineSettings updated;
        lock (_settingsGate)
        {
            updated = change(_settings);
            if (updated == _settings)
                return;
            _settings = updated;
            _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
        SettingsChanged?.Invoke(this, updated);
    }

    private void Flush()
    {
        var snapshot = Current;
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _sessionGate.Wait();
        try
        {
            StopControl();
            _saveTimer.Dispose();
            Flush();
            _sensors.Dispose();
            _load?.Dispose();
            _transport?.Dispose();
        }
        finally
        {
            _sessionGate.Release();
        }
        _sessionGate.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Device}: capability probe\n{Diagnostics}")]
    private partial void LogDetected(string device, string diagnostics);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No Acer gaming firmware (AcerGamingFunction) on this machine")]
    private partial void LogUnsupported();

    [LoggerMessage(Level = LogLevel.Information, Message = "Temperature sources: CPU {Cpu}; GPU {Gpu}")]
    private partial void LogSensors(SensorStatus cpu, SensorStatus gpu);

    [LoggerMessage(Level = LogLevel.Error, Message = "Engine failed to start")]
    private partial void LogStartFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebuilt the session with new capability overrides")]
    private partial void LogRebuilt();

    [LoggerMessage(Level = LogLevel.Information, Message = "GPU mode {Mode} requested, accepted: {Accepted}")]
    private partial void LogGpuMode(GpuMode mode, bool accepted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notice {Kind} {Mode} {Detail}")]
    private partial void LogNotice(NoticeKind kind, OperatingMode? mode, string? detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not save settings to {Path}")]
    private partial void LogSaveFailed(Exception ex, string path);
}
