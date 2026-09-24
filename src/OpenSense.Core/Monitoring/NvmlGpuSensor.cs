using System.Runtime.InteropServices;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// GPU core temperature from the NVIDIA driver (NVML, the library behind nvidia-smi). Call <see cref="Read"/>
/// only while the GPU is already awake: on Optimus laptops a query can power a sleeping GPU back up.
/// </summary>
internal sealed unsafe class NvmlGpuSensor : ITemperatureSensor
{
    private const int Success = 0;
    private const int TemperatureGpu = 0; // NVML_TEMPERATURE_GPU

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlTemperature
    {
        public uint Version;
        public int SensorType;
        public int Temperature;
    }

    private readonly nint _library;
    private readonly delegate* unmanaged<int> _init;
    private readonly delegate* unmanaged<int> _shutdown;
    private readonly delegate* unmanaged<uint, nint*, int> _deviceByIndex;
    private readonly delegate* unmanaged<nint, NvmlTemperature*, int> _temperatureV; // newer drivers only
    private readonly delegate* unmanaged<nint, int, uint*, int> _temperature;
    private bool _initialized;
    private nint _device;

    private NvmlGpuSensor(nint library)
    {
        _library = library;
        _init = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlInit_v2");
        _shutdown = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlShutdown");
        _deviceByIndex = (delegate* unmanaged<uint, nint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetHandleByIndex_v2");
        _temperature = (delegate* unmanaged<nint, int, uint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetTemperature");
        if (NativeLibrary.TryGetExport(library, "nvmlDeviceGetTemperatureV", out var temperatureV))
            _temperatureV = (delegate* unmanaged<nint, NvmlTemperature*, int>)temperatureV;
    }

    public SensorStatus Status { get; } = new(ChipSensor.NvidiaDriver);

    /// <summary>Loads NVML without touching the GPU; null when there is no NVIDIA driver, or one too old for it.</summary>
    public static ITemperatureSensor? TryOpen()
    {
        // Full paths: loading by bare name could pick up an unrelated module that happens to share it.
        if (!NativeLibrary.TryLoad(Path.Combine(Environment.SystemDirectory, "nvml.dll"), out var library) &&
            !NativeLibrary.TryLoad(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvml.dll"), out library))
        {
            return null;
        }
        try
        {
            return new NvmlGpuSensor(library);
        }
        catch (EntryPointNotFoundException)
        {
            NativeLibrary.Free(library);
            return null;
        }
    }

    public double? Read()
    {
        // Initialised on first use, not on open, so a sleeping GPU is never touched at startup.
        if (!_initialized)
        {
            if (_init() != Success)
                return null;
            _initialized = true;
        }
        if (_device == 0)
        {
            nint device;
            if (_deviceByIndex(0, &device) != Success)
                return null;
            _device = device;
        }

        if (_temperatureV != null)
        {
            var reading = new NvmlTemperature { Version = (uint)sizeof(NvmlTemperature) | (1u << 24), SensorType = TemperatureGpu };
            if (_temperatureV(_device, &reading) == Success)
                return reading.Temperature > 0 ? reading.Temperature : null;
        }
        uint celsius;
        if (_temperature(_device, TemperatureGpu, &celsius) == Success)
            return celsius > 0 ? celsius : null;

        _device = 0; // re-acquire next time, e.g. after a driver reset
        return null;
    }

    public void Dispose()
    {
        if (_initialized)
            _shutdown();
        _initialized = false;
        NativeLibrary.Free(_library);
    }
}
