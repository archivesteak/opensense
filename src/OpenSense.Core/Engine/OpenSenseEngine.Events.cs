using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    /// <summary>How many firmware events the diagnostics keep.</summary>
    private const int RecentEventCount = 64;

    private readonly Queue<(DateTime Time, FirmwareEvent Event)> _recentEvents = new();
    private IFirmwareEvents? _events;

    public Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var text = new StringBuilder(_detected.Diagnostics);
        text.AppendLine(_events is null ? "Firmware events: not watched" : "Recent firmware events:");
        lock (_recentEvents)
        {
            foreach (var (time, firmwareEvent) in _recentEvents)
                text.AppendLine(CultureInfo.InvariantCulture, $"  {time:HH:mm:ss} {firmwareEvent}");
        }
        text.AppendLine(GpuClockDiagnostics());
        text.AppendLine("HID devices:");
        foreach (var device in _machine.OpenHid().Enumerate().OrderBy(d => d.VendorId).ThenBy(d => d.ProductId).ThenBy(d => d.UsagePage))
            text.AppendLine(CultureInfo.InvariantCulture, $"  {device}");
        return text.ToString();
    }, cancellationToken);

    private void StartFirmwareEvents()
    {
        var events = _machine.OpenFirmwareEvents();
        events.Raised += OnFirmwareEvent;
        if (events.Start())
        {
            _events = events;
            return;
        }
        events.Raised -= OnFirmwareEvent;
        events.Dispose();
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
                _ = OnModeKeyAsync();
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

    [LoggerMessage(Level = LogLevel.Information, Message = "The firmware's events (APGeEvent) cannot be watched")]
    private partial void LogNoFirmwareEvents();
}
