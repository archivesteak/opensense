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
    private Task? _hidDrain;

    /// <summary>
    /// Opens the HID lights and merges them into what the firmware probe found (their probe goes into the diagnostics).
    /// They are optional: when opening them fails, the firmware's lights stay, and a later HID change tries again.
    /// </summary>
    private DeviceCapabilities DetectHidLights(DeviceCapabilities probed)
    {
        try
        {
            var (lights, capabilities) = OpenHidLights(probed);
            _hidLights = lights;
            return capabilities;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogOptionalFailed(ex, "the HID lights");
            return probed with { Diagnostics = probed.Diagnostics + $"HID lights: could not open them: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}" };
        }
    }

    private (HidLights Lights, DeviceCapabilities Capabilities) OpenHidLights(DeviceCapabilities probed)
    {
        var log = new StringBuilder();
        var lights = HidLights.Open(_hid!, _deviceName, line => log.AppendLine(line));
        try
        {
            var merged = lights.Merge(probed);
            var summary = merged.Lights.Count == 0 ? "none" : string.Join(", ", merged.Lights.Select(l => $"{l.Id} ({l.Backend}, {l.Zones} zones)"));
            log.AppendLine(CultureInfo.InvariantCulture, $"=> lights with HID and USB devices: {summary}; USB keyboard settings: {lights.KeyboardSettings}");
            return (lights, merged with { Diagnostics = probed.Diagnostics + log });
        }
        catch
        {
            lights.Dispose();
            throw;
        }
    }

    private void WatchHid()
    {
        _hidTimer = new Timer(_ => _ = ObserveHidRescanAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _hid!.Changed += OnHidChanged;
    }

    private void OnHidChanged(string path)
    {
        lock (_hidChanges)
        {
            if (Volatile.Read(ref _disposed) != 0 || _hidTimer is null)
                return;
            _hidChanges.Add(path);
            _hidTimer.Change(HidSettleDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task ObserveHidRescanAsync()
    {
        try
        {
            await RescanHidAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogHidRescanFailed(ex);
            lock (_hidChanges)
            {
                if ((_hidDrain is not null || _hidChanges.Count > 0) && Volatile.Read(ref _disposed) == 0)
                    _hidTimer?.Change(HidSettleDelay, Timeout.InfiniteTimeSpan);
            }
            try
            {
                OnNotice(new ControlNotice(NoticeKind.LightingRejected, "HID lighting or keyboard settings are unavailable while discovery retries: " + ex.Message));
            }
            catch (Exception noticeError) when (noticeError is not OutOfMemoryException)
            {
                LogBackgroundFailed(noticeError, "HID recovery notice");
            }
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
            if (_hidDrain is null && !changed.Any(p => _hidLights.Paths.Contains(p) || present.Contains(p)))
                return;
            // Retain this drain across a timeout: a later arrival must wait for the same old owners.
            _hidDrain ??= DrainHidServicesAsync(StopLighting(), StopKeyboard());
            await _hidDrain.WaitAsync(StopWait).ConfigureAwait(false);
            var (lights, detected) = await Task.Run(() => OpenHidLights(_probed with { ModeKey = _detected.ModeKey }))
                .ConfigureAwait(false);
            _hidLights.Dispose();
            _hidLights = lights;
            _detected = detected;
            _capabilities = Current.Overrides.Apply(_detected);
            try
            {
                StartKeyboard(_controller!, Current.Keyboard);
                StartLighting(_controller!, Current.Lighting);
            }
            catch
            {
                // A partially created replacement is an owner too: drain it before the next retry.
                _hidDrain = DrainHidServicesAsync(StopLighting(), StopKeyboard());
                throw;
            }
            _hidDrain = null;
            rebuilt = Snapshot();
        }
        catch
        {
            lock (_hidChanges)
                _hidChanges.UnionWith(changed);
            throw;
        }
        finally
        {
            _sessionGate.Release();
        }
        var lightNames = string.Join(", ", rebuilt.Capabilities.Lights.Select(l => l.Id));
        LogHidRescan(lightNames);
        Rebuilt?.Invoke(this, rebuilt);
    }

    private void StartKeyboard(IDeviceDispatcher dispatcher, KeyboardSettings settings)
    {
        _keyboard = new KeyboardService(dispatcher, _capabilities.Keyboard, _hidLights.KeyboardSettings ? _hidLights.Keyboard : null);
        _keyboard.Notice += OnNotice;
        ObserveWork(_keyboard.ApplyAsync(settings), "keyboard settings");
    }

    private Task StopKeyboard()
    {
        var keyboard = _keyboard;
        if (keyboard is not null)
            keyboard.Notice -= OnNotice;
        _keyboard = null;
        return keyboard?.StopAsync() ?? Task.CompletedTask;
    }

    private async Task DrainHidServicesAsync(Task lightingStopped, Task keyboardStopped)
    {
        try
        {
            await Task.WhenAll(lightingStopped, keyboardStopped).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Both owners are finished even if one rejected a queued request while stopping.
            LogStopFailed(ex);
        }
    }

    private async Task StopHidAsync()
    {
        if (_hid is { } hid)
            hid.Changed -= OnHidChanged;
        lock (_hidChanges)
        {
            _hidTimer?.Dispose();
            _hidTimer = null;
            _hidChanges.Clear();
        }
        if (_hidDrain is { } drain)
            await drain.ConfigureAwait(false);
        if (_lightingWorker is { } worker)
            await worker.StopAsync().ConfigureAwait(false);
        _lightingWorker = null;
        _hidLights.Dispose();
        _hidLights = HidLights.None;
        (_hid as IDisposable)?.Dispose();
        _hid = null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HID devices came or went: lights now {Lights}")]
    private partial void LogHidRescan(string lights);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not rebuild HID lights and keyboard settings; the old work is drained before retrying")]
    private partial void LogHidRescanFailed(Exception ex);

    private sealed class UnavailableHidBus : IHidBus
    {
        public IReadOnlyList<HidDeviceInfo> Enumerate() => [];
        public IHidDevice? Open(HidDeviceInfo device) => null;
        public event Action<string>? Changed { add { } remove { } }
    }
}
