using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    /// <summary>How many firmware events the diagnostics keep.</summary>
    private const int RecentEventCount = 64;

    /// <summary>How many notes on discarded CPU temperature readings the diagnostics keep.</summary>
    private const int RecentSensorNoteCount = 16;

    private readonly Queue<(DateTime Time, FirmwareEvent Event)> _recentEvents = new();
    private readonly Queue<(DateTime Time, string Note)> _recentSensorNotes = new();
    private IFirmwareEvents? _events;

    public Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"OpenSense engine: {Version}");
        text.AppendLine(CultureInfo.InvariantCulture, $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Model: {_deviceName ?? "unknown"}");
        text.AppendLine(CultureInfo.InvariantCulture, $"BIOS: {_biosVersion ?? "unknown"}");
        var profile = AcerFirmwareProfile.For(_deviceName, _biosVersion);
        text.AppendLine(CultureInfo.InvariantCulture, $"Firmware profile: {profile.ModelCode ?? "generic"}; mode reset={profile.RequiresBalancedModeReset}; reliable fan boost readback={profile.FanBoostReadbackReliable}");
        if (_capabilities.BatteryBoostFlag)
            text.AppendLine(CultureInfo.InvariantCulture, $"Battery-boost flag seen on: {Runtime.BatteryBoostSeen}");
        if (_firmware is { } firmware && firmware.FanBehaviors.Any(f => f.Value == FanBehavior.Custom && firmware.FanBoosts.GetValueOrDefault(f.Key) is null))
            text.AppendLine("Startup custom fan boost was unreadable; using the configured fan profile (Auto on a fresh installation).");
        text.AppendLine(CultureInfo.InvariantCulture, $"Keyboard: {_capabilities.Keyboard.PayloadLayout}; route={profile.KeyboardControl}");
        foreach (var light in _capabilities.Lights)
            text.AppendLine(CultureInfo.InvariantCulture, $"Light {light.Id}: {light.Backend}; zones={light.Zones}; effects={string.Join(',', light.Effects.Select(e => e.Effect))}");
        text.AppendLine(_detected.Diagnostics);
        text.AppendLine(_events is null ? "Firmware events: not watched" : "Recent firmware events:");
        lock (_recentEvents)
        {
            foreach (var (time, firmwareEvent) in _recentEvents)
                text.AppendLine(CultureInfo.InvariantCulture, $"  {time:HH:mm:ss} {firmwareEvent}");
        }
        lock (_recentSensorNotes)
        {
            if (_recentSensorNotes.Count > 0)
                text.AppendLine("CPU temperature sensor, recent readings discarded:");
            foreach (var (time, note) in _recentSensorNotes)
                text.AppendLine(CultureInfo.InvariantCulture, $"  {time:HH:mm:ss} {note}");
        }
        text.AppendLine(GpuClockDiagnostics());
        var (cpu, running) = CpuUse.OfThisProcess();
        text.AppendLine(CultureInfo.InvariantCulture, $"Engine process ({Path.GetFileName(Environment.ProcessPath)}): {CpuUse.Describe(cpu, running)}");
        text.Append(_controller?.Timings.Describe() ?? "Control loop: not running" + Environment.NewLine);
        text.AppendLine("HID devices:");
        _sessionGate.Wait(cancellationToken);
        try
        {
            foreach (var device in (_hid?.Enumerate() ?? []).OrderBy(d => d.VendorId).ThenBy(d => d.ProductId).ThenBy(d => d.UsagePage))
                text.AppendLine(CultureInfo.InvariantCulture, $"  {device}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  Enumeration failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _sessionGate.Release();
        }
        return text.ToString();
    }, cancellationToken);

    private void StartFirmwareEvents()
    {
        var events = _machine.OpenFirmwareEvents();
        events.Raised += OnFirmwareEvent;
        _events = events;
        if (events.Start())
            return;
        events.Raised -= OnFirmwareEvent;
        events.Dispose();
        _events = null;
        LogNoFirmwareEvents();
    }

    private void OnFirmwareEvent(FirmwareEvent firmwareEvent)
    {
        lock (_recentEvents)
        {
            _recentEvents.Enqueue((DateTime.Now, firmwareEvent));
            while (_recentEvents.Count > RecentEventCount)
                _recentEvents.Dequeue();
        }
        if (firmwareEvent.Kind == FirmwareEventKind.AcAdapter)
            NotifyPowerSourceChanged();
        if (firmwareEvent.DustDefenderRunning is { } dustDefender)
            _controller?.OnDustDefenderEvent(dustDefender);
        switch (firmwareEvent.Kind)
        {
            case FirmwareEventKind.ModeKey:
                ObserveWork(OnModeKeyAsync(), "mode key");
                break;
            case FirmwareEventKind.BatteryBoost:
                _controller?.OnBatteryBoostEvent();
                break;
        }
        _powerService?.OnFirmwareEvent(firmwareEvent);
    }

    private void StopFirmwareEvents()
    {
        if (_events is not { } events)
            return;
        events.Raised -= OnFirmwareEvent;
        events.Dispose();
        _events = null;
    }

    /// <summary>
    /// Tells the log, and keeps for the diagnostics, what the CPU's sensor says about a reading it had to discard. Runs on the
    /// control thread right after a sample, which is where the sensor is read.
    /// </summary>
    private void NoteSensorGlitch()
    {
        if (_sensors.TakeCpuGlitchNote() is not { } note)
            return;
        LogSensorGlitch(note);
        lock (_recentSensorNotes)
        {
            _recentSensorNotes.Enqueue((DateTime.Now, note));
            while (_recentSensorNotes.Count > RecentSensorNoteCount)
                _recentSensorNotes.Dequeue();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The firmware's events (APGeEvent) cannot be watched")]
    private partial void LogNoFirmwareEvents();
}
