using System.Diagnostics;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Control;

/// <summary>
/// Applies <see cref="LightingConfig"/> to every light: only the lights whose settings changed, latest request wins,
/// and everything again after resume (Acer's agent restores its own lighting when the machine wakes, and USB lights
/// lose theirs in sleep).
/// </summary>
public sealed class LightingService(IReadOnlyList<ILightingBackend> backends, TimeProvider? time = null) : IDisposable
{
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(6);

    /// <summary>Acer's lighting service sends one light no more often than this.</summary>
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(150);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly ServiceLifetime _lifetime = new();
    private LightingConfig? _pending;
    private bool _forget;
    private bool _dimForPower;
    private bool _pumping;
    private Task _pump = Task.CompletedTask;

    // What each light was last told, and when (only touched by the pump, which runs one at a time).
    private readonly Dictionary<string, LightingSettings> _applied = [];
    private readonly Dictionary<string, long> _sentAt = [];

    public IReadOnlyList<LightingDeviceInfo> Devices { get; } = [.. backends.Select(b => b.Device)];

    public LightingConfig Current { get; private set; } = new();

    /// <summary>True while an apply or power-off lighting pass is queued or running.</summary>
    internal bool IsBusy
    {
        get
        {
            lock (_gate)
                return _pending is not null || _pumping || _dimForPower;
        }
    }

    /// <summary>Raised (on a worker thread) when a light rejects a change.</summary>
    public event Action<ControlNotice>? Notice;

    /// <summary>Applies the lights in <paramref name="config"/> whose settings differ from what they were last sent.</summary>
    public Task ApplyAsync(LightingConfig config)
    {
        lock (_gate)
        {
            if (_lifetime.Stopped)
                return Task.CompletedTask;
            Current = config;
            _pending = config;
            _dimForPower = false;
            if (!_pumping)
            {
                _pumping = true;
                _pump = _lifetime.Run(_ => Task.Run(PumpAsync, CancellationToken.None));
            }
            return _pump;
        }
    }

    /// <summary>Records hardware state already in effect (initial adoption or firmware hotkey sync) without rewriting it.</summary>
    internal void SyncApplied(string id, LightingSettings settings)
    {
        lock (_gate)
        {
            if (_lifetime.Stopped)
                return;
            Current = Current.With(id, settings);
            _applied[id] = settings;
        }
    }

    /// <summary>
    /// Temporarily turns off active lights for sleep or shutdown without overwriting <see cref="Current"/>,
    /// so resume and startup restore the user's saved settings (or keep Off if that was saved).
    /// </summary>
    internal Task TurnOffForPowerTransitionAsync()
    {
        lock (_gate)
        {
            if (_lifetime.Stopped)
                return Task.CompletedTask;
            _pending = null;
            _forget = false;
            _dimForPower = true;
            _applied.Clear();
            if (!_pumping)
            {
                _pumping = true;
                _pump = _lifetime.Run(_ => Task.Run(PumpAsync, CancellationToken.None));
            }
            return _pump;
        }
    }

    /// <summary>Forgets what was applied and sends every configured light again.</summary>
    public Task ReapplyAsync()
    {
        lock (_gate)
            _forget = true;
        return ApplyAsync(Current);
    }

    /// <summary>What each light shows now, where it can be read.</summary>
    public Task<IReadOnlyDictionary<string, LightingSettings>> ReadAsync() => _lifetime.Run(ReadCoreAsync);

    /// <summary>Reads one light's current firmware state by <see cref="LightingDeviceInfo.Id"/>, or null if unavailable.</summary>
    internal Task<LightingSettings?> ReadDeviceAsync(string id) => _lifetime.Run(async token =>
    {
        token.ThrowIfCancellationRequested();
        var backend = backends.FirstOrDefault(b => b.Device.Id == id);
        return backend is null ? null : await backend.ReadAsync().ConfigureAwait(false);
    });

    /// <summary>Runs <paramref name="operation"/> tracked by this service's lifetime so stop drains it cleanly.</summary>
    internal Task<T> RunTrackedAsync<T>(Func<CancellationToken, Task<T>> operation) => _lifetime.Run(operation);

    private async Task<IReadOnlyDictionary<string, LightingSettings>> ReadCoreAsync(CancellationToken token)
    {
        var states = new Dictionary<string, LightingSettings>();
        foreach (var backend in backends)
        {
            token.ThrowIfCancellationRequested();
            if (await backend.ReadAsync().ConfigureAwait(false) is { } state)
                states[backend.Device.Id] = state;
        }
        return states;
    }

    /// <summary>The machine woke up: send ours again after Acer's agent has restored its own.</summary>
    /// <returns>Done once they are sent (the engine doesn't wait).</returns>
    public Task OnResume() => _lifetime.Run(async token =>
    {
        await Task.Delay(ResumeDelay, _time, token).ConfigureAwait(false);
        if (!token.IsCancellationRequested)
            await ReapplyAsync().ConfigureAwait(false);
    });

    public Task StopAsync() => _lifetime.StopAsync();

    public void Dispose() => _lifetime.Dispose();

    private async Task PumpAsync()
    {
        try
        {
            await PumpLoopAsync().ConfigureAwait(false);
        }
        catch
        {
            // A light that throws must not leave the pump marked as running forever.
            lock (_gate)
                _pumping = false;
            throw;
        }
    }

    private async Task PumpLoopAsync()
    {
        while (true)
        {
            LightingConfig? next = null;
            bool dim;
            lock (_gate)
            {
                if (_lifetime.Stopped || (!_dimForPower && _pending is null))
                {
                    _pumping = false;
                    return;
                }
                dim = _dimForPower;
                _dimForPower = false;
                if (!dim)
                {
                    next = _pending;
                    _pending = null;
                    if (_forget)
                        _applied.Clear();
                    _forget = false;
                }
            }

            if (dim)
            {
                LightingConfig snapshot;
                lock (_gate)
                    snapshot = Current;
                foreach (var backend in backends)
                {
                    if (_lifetime.Stopped)
                        return;
                    var id = backend.Device.Id;
                    if (snapshot.For(id) is not { } settings || settings.EffectiveBrightness() == 0)
                        continue;
                    if (_sentAt.TryGetValue(id, out var sent) && MinInterval - Stopwatch.GetElapsedTime(sent) is { Ticks: > 0 } wait)
                        await Task.Delay(wait).ConfigureAwait(false);
                    if (_lifetime.Stopped)
                        return;
                    _sentAt[id] = Stopwatch.GetTimestamp();
                    await backend.ApplyAsync(settings with { Brightness = 0 }).ConfigureAwait(false);
                    _applied.Remove(id);
                }
                continue;
            }

            foreach (var backend in backends)
            {
                if (_lifetime.Stopped)
                    return;
                var id = backend.Device.Id;
                if (next!.For(id) is not { } settings || settings.SameAs(_applied.GetValueOrDefault(id)))
                    continue;
                if (_sentAt.TryGetValue(id, out var sent) && MinInterval - Stopwatch.GetElapsedTime(sent) is { Ticks: > 0 } wait)
                    await Task.Delay(wait).ConfigureAwait(false);
                if (_lifetime.Stopped)
                    return;
                _sentAt[id] = Stopwatch.GetTimestamp();
                if (await backend.ApplyAsync(settings).ConfigureAwait(false))
                {
                    _applied[id] = settings;
                }
                else
                {
                    _applied.Remove(id);
                    Notice?.Invoke(new ControlNotice(NoticeKind.LightingRejected) { Light = backend.Device.Location });
                }
            }
        }
    }
}
