using Windows.Wdk.Graphics.Direct3D;
using Windows.Win32.Foundation;
using static Windows.Wdk.PInvoke;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// GPU temperature as the graphics driver reports it to Windows, the reading Task Manager shows: for GPUs without a
/// vendor library of ours (AMD, or NVIDIA without NVML). As with NVML, call <see cref="Read"/> only while the GPU is
/// on. The driver answers in tenths of a degree, and 0 when it has no reading.
/// </summary>
internal sealed unsafe class WindowsGpuSensor : ITemperatureSensor
{
    private static readonly TimeSpan ReopenInterval = TimeSpan.FromSeconds(30);

    private readonly string _hardwareId;
    private uint _adapter;
    private long _nextReopen;

    private WindowsGpuSensor(string hardwareId) => _hardwareId = hardwareId;

    public SensorStatus Status { get; } = new(ChipSensor.GraphicsDriver);

    /// <summary>Opens the GPU's adapter in Windows' graphics kernel, without asking the GPU anything.</summary>
    public static ITemperatureSensor? TryOpen(GpuAdapter gpu, Action<SensorProblem, string?>? fail = null)
    {
        var sensor = new WindowsGpuSensor(gpu.PnpHardwareId);
        var status = sensor.Open(gpu);
        if (Succeeded(status))
            return sensor;
        fail?.Invoke(SensorProblem.GpuAdapterUnavailable, $"0x{status.Value:X8}");
        return null;
    }

    public double? Read()
    {
        if (_adapter == 0 && !Reopen())
            return null;

        D3DKMT_ADAPTER_PERFDATA perf = default;
        var query = new D3DKMT_QUERYADAPTERINFO
        {
            hAdapter = _adapter,
            Type = KMTQUERYADAPTERINFOTYPE.KMTQAITYPE_ADAPTERPERFDATA,
            pPrivateDriverData = &perf,
            PrivateDriverDataSize = (uint)sizeof(D3DKMT_ADAPTER_PERFDATA),
        };
        if (!Succeeded(D3DKMTQueryAdapterInfo(&query)))
        {
            Close(); // the adapter may have gone, e.g. with a driver update
            return null;
        }
        return perf.Temperature > 0 ? perf.Temperature / 10.0 : null;
    }

    private NTSTATUS Open(GpuAdapter gpu)
    {
        var open = new D3DKMT_OPENADAPTERFROMLUID { AdapterLuid = new LUID { LowPart = gpu.LuidLow, HighPart = gpu.LuidHigh } };
        var status = D3DKMTOpenAdapterFromLuid(&open);
        if (Succeeded(status))
            _adapter = open.hAdapter;
        return status;
    }

    /// <summary>A restarted driver gives the GPU a new adapter and LUID: look it up again, now and then.</summary>
    private bool Reopen()
    {
        var now = Environment.TickCount64;
        if (now < _nextReopen)
            return false;
        _nextReopen = now + (long)ReopenInterval.TotalMilliseconds;
        return GpuAdapters.TryEnumerate().FirstOrDefault(a => a.PnpHardwareId == _hardwareId) is { } gpu && Succeeded(Open(gpu));
    }

    private void Close()
    {
        if (_adapter == 0)
            return;
        var close = new D3DKMT_CLOSEADAPTER { hAdapter = _adapter };
        D3DKMTCloseAdapter(&close);
        _adapter = 0;
    }

    private static bool Succeeded(NTSTATUS status) => status.Value >= 0; // NT_SUCCESS

    public void Dispose() => Close();
}
