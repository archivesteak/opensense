using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Engine;

/// <summary>The lights: keyboard backlight, light bars, logos, and the HID and USB lights (<see cref="HidLights"/>).</summary>
public sealed partial class OpenSenseEngine
{
    private static readonly TimeSpan HotkeySyncDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan LightingPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LightingShutdownWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ResumeHoldSafety = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan LocalApplySettle = TimeSpan.FromMilliseconds(800);

    private readonly SemaphoreSlim _lightingSyncGate = new(1, 1);
    private readonly object _lightingSyncTimerGate = new();
    private LightingService? _lighting;
    private IReadOnlyDictionary<string, LightingSettings>? _lightingAtStart;
    private LightingSettings? _baselineKeyboardLighting;
    private int? _baselineTimeoutBrightness;
    private ITimer? _hotkeySyncTimer;
    private ITimer? _lightingPollTimer;
    private DateTimeOffset _syncHoldUntil;
    private int _syncHoldGeneration;
    private volatile bool _suspended;

    internal Task LightingStartupTask { get; private set; } = Task.CompletedTask;

    internal Task ResumeLightingTask { get; private set; } = Task.CompletedTask;

    public Task SetLightingAsync(LightingConfig lighting, CancellationToken cancellationToken = default)
    {
        UpdateSettings(s => s with { Lighting = lighting });
        var holdGeneration = SetSyncHold(LocalApplySettle);
        if (_lighting is not { } service)
        {
            ClearSyncHold(holdGeneration);
            return Task.CompletedTask;
        }
        return ApplyAndCaptureBaselineAsync(service, lighting, holdGeneration);
    }

    public async Task<IReadOnlyDictionary<string, LightingSettings>?> ReadLightingAsync(CancellationToken cancellationToken = default) =>
        _lighting is { } lighting ? await lighting.ReadAsync().ConfigureAwait(false) : null;

    /// <summary>Reads what the lights show (adopting readable lights not yet in settings), then applies the configured ones.</summary>
    private void StartLighting(IDeviceDispatcher dispatcher, LightingConfig settings)
    {
        if (_capabilities.Lights.Any(l => l.Source is not null))
            _lightingWorker ??= new LightingWorker();
        var lighting = new LightingService(LightingBackends.Create(dispatcher, _capabilities, _hidLights, _lightingWorker), _time);
        lighting.Notice += OnNotice;
        _lighting = lighting;
        _lightingAtStart = lighting.ReadAsync().GetAwaiter().GetResult();

        var currentLighting = Current.Lighting;
        var adopted = false;
        if (_lightingAtStart is { Count: > 0 })
        {
            foreach (var (id, state) in _lightingAtStart)
            {
                if (currentLighting.For(id) is null)
                {
                    currentLighting = currentLighting.With(id, state);
                    lighting.SyncApplied(id, state);
                    adopted = true;
                }
            }
        }
        if (adopted)
        {
            UpdateSettings(s => s with { Lighting = currentLighting });
            Flush();
        }

        var holdGeneration = SetSyncHold(LocalApplySettle);
        LightingStartupTask = ApplyAndCaptureBaselineAsync(lighting, Current.Lighting, holdGeneration);
        ObserveWork(LightingStartupTask, "apply lighting");
    }

    private async Task ApplyAndCaptureBaselineAsync(LightingService service, LightingConfig config, int holdGeneration)
    {
        try
        {
            await service.ApplyAsync(config).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) != 0 || _suspended || !ReferenceEquals(_lighting, service))
                return;
            await CaptureLightingBaselineAsync(service).ConfigureAwait(false);
            ClearSyncHold(holdGeneration);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void StartLightingSync()
    {
        lock (_lightingSyncTimerGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            _hotkeySyncTimer ??= _time.CreateTimer(_ => TriggerLightingSync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            if (ShouldPollKeyboardLighting())
                _lightingPollTimer ??= _time.CreateTimer(_ => TriggerLightingSync(), null, LightingPollInterval, LightingPollInterval);
        }
    }

    private void ScheduleHotkeyLightingSync()
    {
        lock (_lightingSyncTimerGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            _hotkeySyncTimer?.Change(HotkeySyncDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void TriggerLightingSync() =>
        ObserveWork(SyncKeyboardLightingAsync(), "sync firmware keyboard lighting");

    private int BeginResumeLightingHold()
    {
        _suspended = false;
        var holdGeneration = SetSyncHold(ResumeHoldSafety);
        lock (_lightingSyncTimerGate)
        {
            if (Volatile.Read(ref _disposed) == 0 && _lightingPollTimer is not null)
                _lightingPollTimer.Change(LightingPollInterval, LightingPollInterval);
        }
        return holdGeneration;
    }

    private async Task CompleteResumeLightingAsync(Task keyboardResume, Task lightingResume, int holdGeneration)
    {
        try
        {
            await Task.WhenAll(keyboardResume, lightingResume).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) != 0 || _suspended || _lighting is not { } service)
                return;
            await CaptureLightingBaselineAsync(service).ConfigureAwait(false);
            ClearSyncHold(holdGeneration);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void SuspendLighting()
    {
        if (Volatile.Read(ref _disposed) != 0 || _state != EngineState.Ready)
            return;
        lock (_lightingSyncTimerGate)
        {
            _hotkeySyncTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _lightingPollTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        _suspended = true;
        try
        {
            if (!SyncKeyboardLightingAsync(ignoreSuspend: true).Wait(LightingShutdownWait))
                LogBackgroundFailed(new TimeoutException("Timed out syncing keyboard lighting before suspend."), "suspend lighting sync");
            Flush();
            if (_lighting is { } lighting && !lighting.TurnOffForPowerTransitionAsync().Wait(LightingShutdownWait))
                LogBackgroundFailed(new TimeoutException("Timed out turning lighting off before suspend."), "suspend lighting off");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogBackgroundFailed(ex, "suspend lighting");
        }
    }

    private void StopLightingSyncAndPreparePowerOff()
    {
        lock (_lightingSyncTimerGate)
        {
            _hotkeySyncTimer?.Dispose();
            _hotkeySyncTimer = null;
            _lightingPollTimer?.Dispose();
            _lightingPollTimer = null;
        }
        if (_state != EngineState.Ready || _lighting is not { } lighting)
            return;
        if (!_suspended)
        {
            if (!SyncKeyboardLightingAsync(ignoreSuspend: true, allowDuringDispose: true).Wait(LightingShutdownWait))
                LogBackgroundFailed(new TimeoutException("Timed out syncing keyboard lighting."), "shutdown lighting sync");
            Flush();
        }
        if (Volatile.Read(ref _powerOffOnShutdown) != 0 && !_suspended)
        {
            if (!lighting.TurnOffForPowerTransitionAsync().Wait(LightingShutdownWait))
                LogBackgroundFailed(new TimeoutException("Timed out turning lighting off for shutdown."), "shutdown lighting off");
        }
    }

    private async Task CaptureLightingBaselineAsync(LightingService service)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        try
        {
            await _lightingSyncGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        try
        {
            if (Volatile.Read(ref _disposed) != 0 || !ReferenceEquals(_lighting, service))
                return;
            var (fwLighting, fwTimeoutBrightness) = await ReadFirmwareKeyboardSnapshotAsync(service).ConfigureAwait(false);
            _baselineKeyboardLighting = fwLighting;
            _baselineTimeoutBrightness = fwTimeoutBrightness;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _lightingSyncGate.Release();
        }
    }

    internal async Task SyncKeyboardLightingAsync(bool ignoreSuspend = false, bool allowDuringDispose = false)
    {
        if ((!allowDuringDispose && Volatile.Read(ref _disposed) != 0) || _state != EngineState.Ready || _lighting is not { } service)
            return;
        if (service.IsBusy || IsSyncHeld(ignoreSuspend))
            return;
        try
        {
            await _lightingSyncGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        try
        {
            if ((!allowDuringDispose && Volatile.Read(ref _disposed) != 0) || !ReferenceEquals(_lighting, service) || service.IsBusy || IsSyncHeld(ignoreSuspend))
                return;
            var (fwLighting, fwTimeoutBrightness) = await ReadFirmwareKeyboardSnapshotAsync(service).ConfigureAwait(false);
            var saved = Current.Lighting.For(EcKeyboardBackend.Id);
            var merged = MergeFirmwareKeyboardChange(saved, fwLighting, fwTimeoutBrightness);
            _baselineKeyboardLighting = fwLighting;
            _baselineTimeoutBrightness = fwTimeoutBrightness;
            if (merged is not null)
            {
                service.SyncApplied(EcKeyboardBackend.Id, merged);
                UpdateSettings(s => s with { Lighting = s.Lighting.With(EcKeyboardBackend.Id, merged) });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _lightingSyncGate.Release();
        }
    }

    private Task<(LightingSettings? Lighting, int? TimeoutBrightness)> ReadFirmwareKeyboardSnapshotAsync(LightingService service) =>
        service.RunTrackedAsync(async _ =>
        {
            var fwLighting = await service.ReadDeviceAsync(EcKeyboardBackend.Id).ConfigureAwait(false);
            int? timeoutBrightness = null;
            if (_controller is { } controller && NeedsTimeoutReadback(_capabilities.Keyboard))
            {
                var raw = await controller.InvokeAsync(d => d.GetBacklightTimeout(_capabilities.Keyboard)?.Brightness).ConfigureAwait(false);
                timeoutBrightness = raw is { } b ? KeyboardProtocol.NearestLevel(b) : null;
            }
            return (fwLighting, timeoutBrightness);
        });

    private LightingSettings? MergeFirmwareKeyboardChange(LightingSettings? saved, LightingSettings? fwLighting, int? fwTimeoutBrightness)
    {
        var baseLighting = _baselineKeyboardLighting;
        var baseTimeout = _baselineTimeoutBrightness;

        var lightingChanged = fwLighting is not null && !fwLighting.SameAs(baseLighting);
        var timeoutChanged = fwTimeoutBrightness is { } tb && baseTimeout is not null && tb != baseTimeout;
        if (!lightingChanged && !timeoutChanged)
            return null;

        if (saved is null && fwLighting is null && fwTimeoutBrightness != 0)
            return null;

        var result = saved ?? fwLighting ?? new LightingSettings();
        if (lightingChanged && fwLighting is not null)
        {
            if (baseLighting is null)
            {
                result = saved is null ? fwLighting : result with
                {
                    Effect = fwLighting.Effect,
                    Brightness = fwLighting.Brightness,
                    Speed = fwLighting.Speed,
                    Direction = fwLighting.Direction,
                    EffectColor = fwLighting.EffectColor,
                    Zones = fwLighting.Zones.Count > 0 ? fwLighting.Zones : result.Zones,
                };
            }
            else
            {
                if (fwLighting.Effect != baseLighting.Effect)
                    result = result with { Effect = fwLighting.Effect };
                if (fwLighting.Brightness != baseLighting.Brightness)
                    result = result with { Brightness = fwLighting.Brightness };
                if (fwLighting.Speed != baseLighting.Speed)
                    result = result with { Speed = fwLighting.Speed };
                if (fwLighting.Direction != baseLighting.Direction)
                    result = result with { Direction = fwLighting.Direction };
                if (fwLighting.EffectColor != baseLighting.EffectColor)
                    result = result with { EffectColor = fwLighting.EffectColor };
                if (fwLighting.Zones.Count > 0 && !fwLighting.Zones.SequenceEqual(baseLighting.Zones))
                {
                    var ignoreBlackReset = _capabilities.Keyboard.ResetsBlackStaticColors
                        && fwLighting.Brightness == 0
                        && result.Effect == LightingEffect.Static
                        && result.Zones.All(z => !z.On || RgbColor.FromHex(z.Color) == default);
                    if (!ignoreBlackReset)
                        result = result with { Zones = fwLighting.Zones };
                }
            }
        }

        if (timeoutChanged && (!lightingChanged || fwLighting is null || fwLighting.Brightness == baseLighting?.Brightness))
            result = result with { Brightness = fwTimeoutBrightness!.Value };

        if (result.Brightness > 0
            && result.Effect == LightingEffect.Static
            && result.Zones.Count > 0
            && result.Zones.All(z => !z.On)
            && ((lightingChanged && fwLighting?.Brightness > 0) || (timeoutChanged && fwTimeoutBrightness > 0)))
        {
            result = result with { Zones = [.. result.Zones.Select(z => z with { On = true })] };
        }

        return saved is not null && result.SameAs(saved) ? null : result;
    }

    private bool ShouldPollKeyboardLighting() =>
        _capabilities.Lights.Any(l => l.Backend is LightingBackendKind.EcKeyboard or LightingBackendKind.EcKeyboardBrightness)
        || (_capabilities.Lights.Any(l => l.Id == EcKeyboardBackend.Id) && NeedsTimeoutReadback(_capabilities.Keyboard));

    private static bool NeedsTimeoutReadback(KeyboardCapabilities keyboard) =>
        keyboard.EcHidBacklightTimeout || keyboard.BacklightHotkey is not null;

    private int? ConfiguredKeyboardBrightness() =>
        Current.Lighting.For(EcKeyboardBackend.Id)?.EffectiveBrightness();

    private int SetSyncHold(TimeSpan duration)
    {
        lock (_lightingSyncTimerGate)
        {
            _syncHoldGeneration++;
            _syncHoldUntil = _time.GetUtcNow() + duration;
            return _syncHoldGeneration;
        }
    }

    private void ClearSyncHold(int generation)
    {
        lock (_lightingSyncTimerGate)
        {
            if (_syncHoldGeneration == generation)
                _syncHoldUntil = DateTimeOffset.MinValue;
        }
    }

    private bool IsSyncHeld(bool ignoreSuspend)
    {
        if (!ignoreSuspend && _suspended)
            return true;
        lock (_lightingSyncTimerGate)
            return _time.GetUtcNow() < _syncHoldUntil;
    }

    private Task StopLighting()
    {
        var lighting = _lighting;
        if (lighting is not null)
            lighting.Notice -= OnNotice;
        _lighting = null;
        return lighting?.StopAsync() ?? Task.CompletedTask;
    }
}
