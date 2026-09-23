using System.Text.RegularExpressions;
using Windows.Win32.System.Performance;

namespace OpenSense.Core.Monitoring;

public interface ILoadMonitor : IDisposable
{
    string? CpuName { get; }
    string? GpuName { get; }

    /// <summary>CPU and discrete-GPU utilisation in percent, sampled since the previous call.</summary>
    (double? Cpu, double? Gpu) Sample();
}

/// <summary>CPU load from "% Processor Utility" (matches Task Manager), GPU load from "GPU Engine" 3D counters.</summary>
public sealed partial class LoadMonitor : ILoadMonitor
{
    private readonly PdhQuery _query = new();
    private readonly PDH_HCOUNTER? _cpu;
    private readonly PDH_HCOUNTER? _gpu3d;
    private readonly GpuAdapter? _discrete;

    public string? CpuName { get; }
    public string? GpuName => _discrete?.Name;

    public LoadMonitor()
    {
        _cpu = _query.Add(@"\Processor Information(_Total)\% Processor Utility")
            ?? _query.Add(@"\Processor(_Total)\% Processor Time");
        _gpu3d = _query.Add(@"\GPU Engine(*engtype_3D)\Utilization Percentage");
        try
        {
            _discrete = GpuAdapters.FindDiscrete(GpuAdapters.Enumerate());
        }
        catch (Exception)
        {
            _discrete = null;
        }
        CpuName = ReadCpuName();
        _query.Collect(); // rate counters need a baseline sample
    }

    public (double? Cpu, double? Gpu) Sample()
    {
        if (!_query.Collect())
            return (null, null);

        double? cpu = _cpu is { } c && PdhQuery.Value(c) is { } v ? Math.Clamp(v, 0, 100) : null;
        return (cpu, SampleGpu());
    }

    private double? SampleGpu()
    {
        if (_gpu3d is not { } counter)
            return null;
        var perAdapter = new Dictionary<string, double>();
        foreach (var (instance, value) in PdhQuery.Values(counter))
        {
            var luid = LuidPattern().Match(instance);
            if (luid.Success)
                perAdapter[luid.Value] = perAdapter.GetValueOrDefault(luid.Value) + value;
        }
        if (perAdapter.Count == 0)
            return 0;
        var load = _discrete is not null
            ? perAdapter.GetValueOrDefault(_discrete.CounterLuid)
            : perAdapter.Values.Max();
        return Math.Clamp(load, 0, 100);
    }

    private static string? ReadCpuName()
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string)?.Trim();
    }

    [GeneratedRegex("luid_0x[0-9A-Fa-f]{8}_0x[0-9A-Fa-f]{8}")]
    private static partial Regex LuidPattern();

    public void Dispose() => _query.Dispose();
}
