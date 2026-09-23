using OpenSense.Core.Hardware;

namespace OpenSense.Core.Control;

/// <summary>Runs work on the thread that owns the firmware (see <see cref="FanControlService"/>).</summary>
public interface IDeviceDispatcher
{
    Task<T> InvokeAsync<T>(Func<AcerDevice, T> action);
}

/// <summary>
/// Applies <see cref="KeyboardSettings"/> to the firmware: only what changed, latest request wins,
/// and everything again after resume (Acer's agent restores its own lighting when the machine wakes).
/// </summary>
public sealed class KeyboardService
{
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(6);

    private readonly IDeviceDispatcher _dispatcher;
    private readonly KeyboardCapabilities _caps;
    private readonly object _gate = new();

    private KeyboardSettings? _pending;
    private bool _pumping;
    private Task _pump = Task.CompletedTask;

    // What the firmware was last told (only touched on the dispatcher thread).
    private LightingSettings? _appliedLighting;
    private bool? _appliedAutoOff, _appliedWindowsKey, _appliedOverdrive;

    public KeyboardService(IDeviceDispatcher dispatcher, KeyboardCapabilities capabilities)
    {
        _dispatcher = dispatcher;
        _caps = capabilities;
    }

    public KeyboardSettings Current { get; private set; } = new();

    /// <summary>Raised (on a worker thread) when the firmware rejects a change.</summary>
    public event Action<ControlNotice>? Notice;

    public Task<KeyboardState> ReadStateAsync() => _dispatcher.InvokeAsync(d => KeyboardState.Read(d, _caps));

    /// <summary>Applies the members of <paramref name="settings"/> that differ from what was last applied.</summary>
    public Task ApplyAsync(KeyboardSettings settings)
    {
        lock (_gate)
        {
            Current = settings;
            _pending = settings;
            if (!_pumping)
            {
                _pumping = true;
                _pump = Task.Run(PumpAsync);
            }
            return _pump;
        }
    }

    /// <summary>Forgets what was applied and sends every configured setting again.</summary>
    public Task ReapplyAsync()
    {
        return _dispatcher.InvokeAsync(_ =>
        {
            _appliedLighting = null;
            _appliedAutoOff = _appliedWindowsKey = _appliedOverdrive = null;
            return true;
        }).ContinueWith(_ => ApplyAsync(Current), TaskScheduler.Default).Unwrap();
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            KeyboardSettings next;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _pumping = false;
                    return;
                }
                next = _pending;
                _pending = null;
            }
            var failures = await _dispatcher.InvokeAsync(d => ApplyChanges(d, next)).ConfigureAwait(false);
            foreach (var failure in failures)
                Notice?.Invoke(new ControlNotice(failure));
        }
    }

    private List<NoticeKind> ApplyChanges(AcerDevice device, KeyboardSettings settings)
    {
        var failures = new List<NoticeKind>();

        if (_caps.RgbBacklight && settings.Lighting is { } lighting && !SameLighting(lighting, _appliedLighting))
        {
            if (ApplyLighting(device, lighting))
                _appliedLighting = lighting;
            else
                failures.Add(NoticeKind.LightingRejected);
        }

        if (_caps.BacklightHotkey is { } hotkey && settings.BacklightAutoOff is { } autoOff && autoOff != _appliedAutoOff)
        {
            var brightness = device.GetBacklightTimeout(hotkey)?.Brightness ?? 100;
            if (device.SetBacklightTimeout(hotkey, brightness, autoOff ? KeyboardProtocol.AutoOffSeconds : 0))
                _appliedAutoOff = autoOff;
            else
                failures.Add(NoticeKind.BacklightTimeoutRejected);
        }

        if (_caps.WindowsKey && settings.WindowsKey is { } winKey && winKey != _appliedWindowsKey)
        {
            if (device.SetWindowsKeyEnabled(winKey))
                _appliedWindowsKey = winKey;
            else
                failures.Add(NoticeKind.WindowsKeyRejected);
        }

        if (_caps.LcdOverdrive && settings.LcdOverdrive is { } overdrive && overdrive != _appliedOverdrive)
        {
            if (device.SetLcdOverdrive(overdrive))
                _appliedOverdrive = overdrive;
            else
                failures.Add(NoticeKind.LcdOverdriveRejected);
        }

        return failures;
    }

    private bool ApplyLighting(AcerDevice device, LightingSettings lighting)
    {
        if (lighting.Effect != KeyboardEffect.Static)
        {
            return device.SetKeyboardBacklight(lighting.Effect, lighting.Speed, lighting.Brightness, lighting.Direction,
                Adjust(RgbColor.FromHex(lighting.EffectColor)));
        }

        var zones = Enumerable.Range(0, _caps.Zones).Select(lighting.Zone).ToList();
        var ok = device.SetZonesEnabled([.. zones.Select(z => z.On)], _caps.ArrayZoneCommand);
        ok &= device.SetKeyboardBacklight(KeyboardEffect.Static, 0, lighting.Brightness, KeyboardDirection.Right, default);
        for (var i = 0; i < zones.Count; i++)
        {
            if (zones[i].On)
                ok &= device.SetZoneColor(i + 1, Adjust(RgbColor.FromHex(zones[i].Color)));
        }
        return ok;
    }

    /// <summary>NitroSense's per-model colour correction.</summary>
    private RgbColor Adjust(RgbColor color)
    {
        static byte Scale(byte channel, double factor) => (byte)Math.Clamp(Math.Floor(channel * factor), 0, 255);
        var a = _caps.ColorAdjust;
        return a.Count < 3 ? color : new RgbColor(Scale(color.R, a[0]), Scale(color.G, a[1]), Scale(color.B, a[2]));
    }

    private static bool SameLighting(LightingSettings a, LightingSettings? b) =>
        b is not null && a with { Zones = b.Zones } == b && a.Zones.SequenceEqual(b.Zones);

    /// <summary>The machine woke up: Acer's agent restores its own lighting, so send ours again after it.</summary>
    public void OnResume() => _ = Task.Delay(ResumeDelay).ContinueWith(_ => ReapplyAsync(), TaskScheduler.Default).Unwrap();
}
