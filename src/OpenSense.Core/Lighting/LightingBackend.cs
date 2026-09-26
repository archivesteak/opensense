using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>Drives one light. Each backend runs its calls on the thread that owns its device.</summary>
public interface ILightingBackend
{
    LightingDeviceInfo Device { get; }

    /// <summary>Sends <paramref name="settings"/> to the light; false when it refused.</summary>
    Task<bool> ApplyAsync(LightingSettings settings);

    /// <summary>What the light shows now, as far as it can be read; null when it can't.</summary>
    Task<LightingSettings?> ReadAsync();
}

/// <summary>What every backend shares: its device and the clamping of values to what the device takes.</summary>
public abstract class LightingBackendBase(LightingDeviceInfo device)
{
    public LightingDeviceInfo Device { get; } = device;

    /// <summary>The nearest of the device's brightness steps.</summary>
    protected int NearestBrightness(int value) => Device.BrightnessLevels.MinBy(l => Math.Abs(l - value));

    /// <summary>Speed within what <paramref name="effect"/> takes on this device.</summary>
    protected int ClampSpeed(LightingEffect effect, int speed) =>
        Device.Traits(effect) is { } traits ? Math.Clamp(speed, traits.MinSpeed, traits.MaxSpeed) : speed;

    /// <summary>Random colours where the settings ask for them and the effect takes them here.</summary>
    protected bool Random(LightingSettings settings) => settings.RandomColor && Device.Traits(settings.Effect) is { RandomColor: true };
}

/// <summary>A light on the embedded controller: calls go through the firmware's thread.</summary>
public abstract class EcLightingBackend(IDeviceDispatcher dispatcher, LightingDeviceInfo device) : LightingBackendBase(device), ILightingBackend
{
    public Task<bool> ApplyAsync(LightingSettings settings) => dispatcher.InvokeAsync(d => Apply(d, settings));

    public Task<LightingSettings?> ReadAsync() => dispatcher.InvokeAsync(Read);

    protected abstract bool Apply(AcerDevice device, LightingSettings lighting);

    protected abstract LightingSettings? Read(AcerDevice device);
}

/// <summary>A HID or USB light: calls go through the lighting thread. None of them can be read back.</summary>
public abstract class WorkerLightingBackend(LightingWorker worker, LightingDeviceInfo device) : LightingBackendBase(device), ILightingBackend
{
    public Task<bool> ApplyAsync(LightingSettings settings) => worker.InvokeAsync(() => Device.Offers(settings.Effect) && Apply(settings));

    public Task<LightingSettings?> ReadAsync() => Task.FromResult<LightingSettings?>(null);

    /// <summary>Sends <paramref name="lighting"/>, whose effect the device offers.</summary>
    protected abstract bool Apply(LightingSettings lighting);
}
