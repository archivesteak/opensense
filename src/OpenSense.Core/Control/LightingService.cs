using System.Diagnostics;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Control;

/// <summary>
/// Applies <see cref="LightingConfig"/> to every light: only the lights whose settings changed, latest request wins,
/// and everything again after resume (Acer's agent restores its own lighting when the machine wakes, and USB lights
/// lose theirs in sleep).
/// </summary>
public sealed class LightingService(IReadOnlyList<ILightingBackend> backends, TimeProvider? time = null)
{
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(6);

    /// <summary>Acer's lighting service sends one light no more often than this.</summary>
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(150);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _gate = new();
    private LightingConfig? _pending;
    private bool _forget;
    private bool _pumping;
    private Task _pump = Task.CompletedTask;

    // What each light was last told, and when (only touched by the pump, which runs one at a time).
    private readonly Dictionary<string, LightingSettings> _applied = [];
    private readonly Dictionary<string, long> _sentAt = [];

    public IReadOnlyList<LightingDeviceInfo> Devices { get; } = [.. backends.Select(b => b.Device)];

    public LightingConfig Current { get; private set; } = new();

    /// <summary>Raised (on a worker thread) when a light rejects a change.</summary>
    public event Action<ControlNotice>? Notice;

    /// <summary>Applies the lights in <paramref name="config"/> whose settings differ from what they were last sent.</summary>
    public Task ApplyAsync(LightingConfig config)
    {
        lock (_gate)
        {
            Current = config;
            _pending = config;
            if (!_pumping)
            {
                _pumping = true;
                _pump = Task.Run(PumpAsync);
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
    public async Task<IReadOnlyDictionary<string, LightingSettings>> ReadAsync()
    {
        var states = new Dictionary<string, LightingSettings>();
        foreach (var backend in backends)
        {
            if (await backend.ReadAsync().ConfigureAwait(false) is { } state)
                states[backend.Device.Id] = state;
        }
        return states;
    }

    /// <summary>The machine woke up: send ours again after Acer's agent has restored its own.</summary>
    /// <returns>Done once they are sent (the engine doesn't wait).</returns>
    public Task OnResume() => Task.Delay(ResumeDelay, _time).ContinueWith(_ => ReapplyAsync(), TaskScheduler.Default).Unwrap();

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
            LightingConfig next;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _pumping = false;
                    return;
                }
                next = _pending;
                _pending = null;
                if (_forget)
                    _applied.Clear();
                _forget = false;
            }

            foreach (var backend in backends)
            {
                var id = backend.Device.Id;
                if (next.For(id) is not { } settings || settings.SameAs(_applied.GetValueOrDefault(id)))
                    continue;
                if (_sentAt.TryGetValue(id, out var sent) && MinInterval - Stopwatch.GetElapsedTime(sent) is { Ticks: > 0 } wait)
                    await Task.Delay(wait).ConfigureAwait(false);
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
