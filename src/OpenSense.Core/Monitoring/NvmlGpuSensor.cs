using System.Runtime.InteropServices;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// GPU core temperature from the NVIDIA driver (NVML, the library behind nvidia-smi). Call <see cref="Read"/>
/// only while the GPU is already awake: on Optimus laptops a query can power a sleeping GPU back up.
/// </summary>
internal sealed unsafe class NvmlGpuSensor : ITemperatureSensor
{
    private const int TemperatureGpu = 0; // NVML_TEMPERATURE_GPU

    /// <summary>
    /// NVML_TEMPERATURE_THRESHOLD_GPS_CURR: where GPU Boost starts lowering the clocks (87 °C on the RTX 3070 Laptop GPU,
    /// the target temperature nvidia-smi lists). Its slowdown threshold (98 °C there) is the hard brake above it.
    /// </summary>
    private const int ThresholdPerformanceScaling = 7;

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlTemperature
    {
        public uint Version;
        public int SensorType;
        public int Temperature;
    }

    private readonly NvmlLibrary _nvml;
    private readonly delegate* unmanaged<nint, NvmlTemperature*, int> _temperatureV; // newer drivers only
    private readonly delegate* unmanaged<nint, int, uint*, int> _temperature;
    private readonly delegate* unmanaged<nint, int, uint*, int> _threshold;
    private bool _limitAsked;

    private NvmlGpuSensor(NvmlLibrary nvml, nint temperature)
    {
        _nvml = nvml;
        _temperature = (delegate* unmanaged<nint, int, uint*, int>)temperature;
        _temperatureV = (delegate* unmanaged<nint, NvmlTemperature*, int>)nvml.Export("nvmlDeviceGetTemperatureV");
        _threshold = (delegate* unmanaged<nint, int, uint*, int>)nvml.Export("nvmlDeviceGetTemperatureThreshold");
    }

    public SensorStatus Status { get; } = new(ChipSensor.NvidiaDriver);

    /// <summary>Asked once, with the first reading: only then is the GPU known to be awake.</summary>
    public int? Limit { get; private set; }

    /// <summary>The sensor, on a driver that has it; the library stays <paramref name="nvml"/>'s owner's to dispose.</summary>
    public static NvmlGpuSensor? TryOpen(NvmlLibrary nvml) =>
        nvml.Export("nvmlDeviceGetTemperature") is not 0 and var temperature ? new NvmlGpuSensor(nvml, temperature) : null;

    public double? Read()
    {
        // NVML is initialised on first use, not on open, so a sleeping GPU is never touched at startup.
        if (!_nvml.TryGetDevice(out var device))
            return null;

        if (!_limitAsked && _threshold != null)
        {
            _limitAsked = true;
            uint limit;
            if (_threshold(device, ThresholdPerformanceScaling, &limit) == NvmlLibrary.Success && limit is >= 60 and <= 110)
                Limit = (int)limit;
        }

        if (_temperatureV != null)
        {
            var reading = new NvmlTemperature { Version = (uint)sizeof(NvmlTemperature) | (1u << 24), SensorType = TemperatureGpu };
            if (_temperatureV(device, &reading) == NvmlLibrary.Success)
                return reading.Temperature > 0 ? reading.Temperature : null;
        }
        uint celsius;
        if (_temperature(device, TemperatureGpu, &celsius) == NvmlLibrary.Success)
            return celsius > 0 ? celsius : null;

        _nvml.ForgetDevice(); // look it up again next time, e.g. after a driver reset
        _limitAsked = false;
        return null;
    }

    public void Dispose()
    {
        // The library belongs to DirectSensors, which the clock offsets share it with.
    }
}
