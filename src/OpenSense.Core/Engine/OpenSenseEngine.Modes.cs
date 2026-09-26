using Microsoft.Extensions.Logging;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    /// <summary>
    /// The Mode key (firmware event 7): the firmware leaves switching to software, so this does what Acer's software
    /// does (<see cref="OperatingModePolicy.NextForKey"/>), keeps the choice in the profile and tells the clients, which
    /// show it as a toast. The first press also shows that the laptop has the key.
    /// </summary>
    private async Task OnModeKeyAsync()
    {
        try
        {
            await NoteModeKeyAsync().ConfigureAwait(false);
            if (_controller is not { } controller || !_capabilities.HasOperatingModes)
                return;
            var profile = Current.Profile;
            var press = await controller.PressModeKeyAsync(profile).ConfigureAwait(false);
            LogModeKey(press.Current, press.Next, press.Limit);
            if (press.Next is not { } next)
            {
                // Nowhere to go (Turbo on battery, or one mode only): say why Turbo can't, or what runs.
                if (OperatingModePolicy.TurboBlocked(profile, press.Limit))
                    NoticeRaised?.Invoke(this, new ControlNotice(NoticeKind.TurboUnavailable) { PowerLimit = press.Limit });
                else if (press.Current is { } current)
                    NoticeRaised?.Invoke(this, new ControlNotice(NoticeKind.OperatingModeSwitchedByKey) { OperatingMode = current, PowerLimit = press.Limit });
                return;
            }
            UpdateSettings(s => s with { Profile = OperatingModePolicy.WithKeyChoice(s.Profile, press.Current, next, press.Limit) });
            controller.Update(Current.Profile);
            NoticeRaised?.Invoke(this, new ControlNotice(NoticeKind.OperatingModeSwitchedByKey) { OperatingMode = next, PowerLimit = press.Limit });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogModeKeyFailed(ex);
        }
    }

    /// <summary>Remembers that the laptop has a Mode key, and gives the clients the capability.</summary>
    private async Task NoteModeKeyAsync()
    {
        if (Runtime.ModeKeySeen)
            return;
        UpdateRuntime(r => r with { ModeKeySeen = true });
        EngineSnapshot rebuilt;
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _detected = _detected with { ModeKey = true };
            _capabilities = _capabilities with { ModeKey = true };
            rebuilt = Snapshot();
        }
        finally
        {
            _sessionGate.Release();
        }
        LogModeKeyFound();
        Rebuilt?.Invoke(this, rebuilt);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Mode key: {Current} -> {Next} ({Limit})")]
    private partial void LogModeKey(OperatingMode? current, OperatingMode? next, PowerLimit limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "The laptop has a Mode key (first press seen)")]
    private partial void LogModeKeyFound();

    [LoggerMessage(Level = LogLevel.Error, Message = "The Mode key could not switch the mode")]
    private partial void LogModeKeyFailed(Exception ex);
}
