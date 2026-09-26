using OpenSense.Core.Control;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Engine;

/// <summary>The lights: keyboard backlight, light bars, logos, and the HID and USB lights (<see cref="HidLights"/>).</summary>
public sealed partial class OpenSenseEngine
{
    private LightingService? _lighting;
    private IReadOnlyDictionary<string, LightingSettings>? _lightingAtStart;

    public Task SetLightingAsync(LightingConfig lighting, CancellationToken cancellationToken = default)
    {
        UpdateSettings(s => s with { Lighting = lighting });
        return _lighting?.ApplyAsync(lighting) ?? Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<string, LightingSettings>?> ReadLightingAsync(CancellationToken cancellationToken = default) =>
        _lighting is { } lighting ? await lighting.ReadAsync().ConfigureAwait(false) : null;

    /// <summary>Reads what the lights show (for lights the user hasn't set yet), then applies the ones they have.</summary>
    private void StartLighting(IDeviceDispatcher dispatcher, LightingConfig settings)
    {
        if (_capabilities.Lights.Any(l => l.Source is not null))
            _lightingWorker ??= new LightingWorker();
        var lighting = new LightingService(LightingBackends.Create(dispatcher, _capabilities, _hidLights, _lightingWorker));
        lighting.Notice += OnNotice;
        _lightingAtStart = lighting.ReadAsync().GetAwaiter().GetResult();
        _lighting = lighting;
        _ = lighting.ApplyAsync(settings);
    }

    private void StopLighting()
    {
        if (_lighting is { } lighting)
            lighting.Notice -= OnNotice;
        _lighting = null;
    }
}
