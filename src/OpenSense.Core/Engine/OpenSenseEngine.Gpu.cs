using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    /// <summary>What the GPU's driver said about clock offsets last time, if it was this GPU.</summary>
    private GpuClockRecord? KnownGpuClocks() =>
        _sensors.GpuClocks is { } clocks && Runtime.GpuClocks is { } record && record.Device == clocks.Device ? record : null;

    /// <summary>Remembered, so the limits are known before the GPU is next on (asking would wake it).</summary>
    private void OnGpuClockLimits(GpuClockLimits? limits)
    {
        if (_sensors.GpuClocks is not { } clocks)
            return;
        UpdateRuntime(r => r with { GpuClocks = new GpuClockRecord(clocks.Device, clocks.DriverVersion, limits) });
        if (limits is null)
            LogNoGpuClocks(clocks.Device, clocks.DriverVersion);
        else
            LogGpuClockLimits(clocks.Device, clocks.DriverVersion, limits);
    }

    /// <summary>Kept on disk at once, so a crash while overclocked is found at the next start.</summary>
    private void OnGpuOverclockApplied(bool applied) => UpdateRuntime(r => r with { GpuOverclockApplied = applied });

    /// <summary>
    /// The last run never stopped while OpenSense's offsets were in the driver: Windows crashed, froze or lost power,
    /// maybe because of them. Applied again at every start they could keep the laptop from ever staying up, so
    /// overclocking (and the firmware's) is switched off until the user switches it on again.
    /// </summary>
    private void SwitchOffGpuOverclockAfterCrash()
    {
        LogGpuOverclockOffAfterCrash();
        UpdateSettings(s => s with
        {
            Profile = s.Profile with
            {
                GpuClocks = s.Profile.GpuClocks with { Enabled = false, FollowFirmware = false, TurnedOffAfterCrash = true },
            },
        });
        Flush();
        UpdateRuntime(r => r with { GpuOverclockApplied = false });
    }

    private string GpuClockDiagnostics()
    {
        if (_sensors.GpuClocks is not { } clocks)
            return "GPU clock offsets: no NVIDIA driver control";
        var record = KnownGpuClocks();
        var driver = clocks.DriverVersion ?? record?.Driver ?? "not asked yet";
        var limits = record is null ? "not known yet" : record.Limits?.ToString() ?? "none";
        return string.Create(CultureInfo.InvariantCulture,
            $"GPU clock offsets: {clocks.Device}, driver {driver}, limits {limits}, now {_latest?.GpuClocks?.ToString() ?? "-"}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "GPU {Device}, driver {Driver}: clock offsets {Limits}")]
    private partial void LogGpuClockLimits(string device, string? driver, GpuClockLimits limits);

    [LoggerMessage(Level = LogLevel.Information, Message = "GPU {Device}, driver {Driver}: no clock offsets")]
    private partial void LogNoGpuClocks(string device, string? driver);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The last run ended without stopping while the GPU was overclocked; overclocking switched off")]
    private partial void LogGpuOverclockOffAfterCrash();
}
