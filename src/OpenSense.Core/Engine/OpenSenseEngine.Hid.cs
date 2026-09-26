using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Engine;

/// <summary>HID and USB devices: the lights on them, a USB keyboard's settings, and finding them again when they come or go.</summary>
public sealed partial class OpenSenseEngine
{
    /// <summary>A resume or a plugged-in device brings a burst of arrivals and removals: wait for them to settle.</summary>
    private static readonly TimeSpan HidSettleDelay = TimeSpan.FromSeconds(2);

    private IHidBus? _hid;
    private HidLights _hidLights = HidLights.None;
    private LightingWorker? _lightingWorker;

    /// <summary>What the firmware probe found, before the HID lights were added (they are merged in again after a rescan).</summary>
    private DeviceCapabilities _probed = DeviceCapabilities.None;

    private Timer? _hidTimer;
    private readonly HashSet<string> _hidChanges = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Opens the HID lights and merges them into what the firmware probe found (their probe goes into the diagnostics).</summary>
    private DeviceCapabilities DetectHidLights(DeviceCapabilities probed)
    {
        var log = new StringBuilder();
        _hidLights = HidLights.Open(_hid!, _deviceName, line => log.AppendLine(line));
        var merged = _hidLights.Merge(probed);
        var summary = merged.Lights.Count == 0 ? "none" : string.Join(", ", merged.Lights.Select(l => $"{l.Id} ({l.Backend}, {l.Zones} zones)"));
        log.AppendLine(CultureInfo.InvariantCulture, $"=> lights with HID and USB devices: {summary}; USB keyboard settings: {_hidLights.KeyboardSettings}");
        return merged with { Diagnostics = probed.Diagnostics + log };
    }

    private void WatchHid()
    {
        _hidTimer = new Timer(_ => _ = RescanHidAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _hid!.Changed += OnHidChanged;
    }

    private void OnHidChanged(string path)
    {
        lock (_hidChanges)
        {
            _hidChanges.Add(path);
            _hidTimer?.Change(HidSettleDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// After HID interfaces came or went: when one of them is (or would be) a light's or the USB keyboard's, open them all
    /// again, rebuild the lights and the keyboard settings, and re-apply them (a device that comes back has lost its state).
    /// </summary>
    private async Task RescanHidAsync()
    {
        string[] changed;
        lock (_hidChanges)
        {
            changed = [.. _hidChanges];
            _hidChanges.Clear();
        }
        EngineSnapshot rebuilt;
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_state != EngineState.Ready || _hid is null || Volatile.Read(ref _disposed) != 0)
                return;
            var present = _hid.Enumerate().Where(HidLights.Relevant).Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!changed.Any(p => _hidLights.Paths.Contains(p) || present.Contains(p)))
                return;
            await Task.Run(() =>
            {
                StopLighting();
                StopKeyboard();
                // After what the lighting thread still has queued for them.
                var old = _hidLights;
                _lightingWorker?.InvokeAsync(() =>
                {
                    old.Dispose();
                    return true;
                }).Wait();
                _detected = DetectHidLights(_probed with { ModeKey = _detected.ModeKey });
                _capabilities = Current.Overrides.Apply(_detected);
                StartKeyboard(_controller!, Current.Keyboard);
                StartLighting(_controller!, Current.Lighting);
            }).ConfigureAwait(false);
            rebuilt = Snapshot();
        }
        finally
        {
            _sessionGate.Release();
        }
        var lights = string.Join(", ", rebuilt.Capabilities.Lights.Select(l => l.Id));
        LogHidRescan(lights);
        Rebuilt?.Invoke(this, rebuilt);
    }

    private void StartKeyboard(IDeviceDispatcher dispatcher, KeyboardSettings settings)
    {
        _keyboard = new KeyboardService(dispatcher, _capabilities.Keyboard, _hidLights.KeyboardSettings ? _hidLights.Keyboard : null);
        _keyboard.Notice += OnNotice;
        _ = _keyboard.ApplyAsync(settings);
    }

    private void StopKeyboard()
    {
        if (_keyboard is { } keyboard)
            keyboard.Notice -= OnNotice;
        _keyboard = null;
    }

    private void StopHid()
    {
        if (_hid is { } hid)
            hid.Changed -= OnHidChanged;
        _hidTimer?.Dispose();
        _hidTimer = null;
        _lightingWorker?.Dispose();
        _hidLights.Dispose();
        _hidLights = HidLights.None;
        (_hid as IDisposable)?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HID devices came or went: lights now {Lights}")]
    private partial void LogHidRescan(string lights);
}
