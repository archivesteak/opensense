using System.Runtime.InteropServices;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// NVIDIA's management library (NVML, the library behind nvidia-smi), shared by the GPU temperature sensor and the
/// clock offsets. Loading it touches nothing; it is initialised on first use, which powers up a sleeping Optimus GPU,
/// so callers use it only while the GPU is on. Used from the control thread.
/// </summary>
internal sealed unsafe class NvmlLibrary : IDisposable
{
    public const int Success = 0;
    public const int NotSupported = 3; // NVML_ERROR_NOT_SUPPORTED

    [StructLayout(LayoutKind.Sequential)]
    private struct PciInfo
    {
        public fixed byte BusIdLegacy[16];
        public uint Domain;
        public uint Bus;
        public uint Device;
        public uint PciDeviceId; // device id << 16 | vendor id
        public uint PciSubSystemId;
        public fixed byte BusId[32];
    }

    private readonly nint _library;
    private readonly GpuAdapter? _gpu;
    private readonly delegate* unmanaged<int> _init;
    private readonly delegate* unmanaged<int> _shutdown;
    private readonly delegate* unmanaged<uint*, int> _deviceCount;
    private readonly delegate* unmanaged<uint, nint*, int> _deviceByIndex;
    private readonly delegate* unmanaged<nint, PciInfo*, int> _pciInfo;
    private readonly delegate* unmanaged<byte*, uint, int> _driverVersion;
    private bool _initialized;
    private nint _device;

    private NvmlLibrary(nint library, GpuAdapter? gpu)
    {
        _library = library;
        _gpu = gpu;
        _init = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlInit_v2");
        _shutdown = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlShutdown");
        _deviceCount = (delegate* unmanaged<uint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetCount_v2");
        _deviceByIndex = (delegate* unmanaged<uint, nint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetHandleByIndex_v2");
        _pciInfo = (delegate* unmanaged<nint, PciInfo*, int>)Export("nvmlDeviceGetPciInfo_v3");
        _driverVersion = (delegate* unmanaged<byte*, uint, int>)Export("nvmlSystemGetDriverVersion");
    }

    /// <summary>Loads NVML for <paramref name="gpu"/> (the discrete GPU, if found) without touching the GPU; null without an NVIDIA driver, or one too old.</summary>
    public static NvmlLibrary? TryOpen(GpuAdapter? gpu)
    {
        // Full paths: loading by bare name could pick up an unrelated module that happens to share it.
        if (!NativeLibrary.TryLoad(Path.Combine(Environment.SystemDirectory, "nvml.dll"), out var library) &&
            !NativeLibrary.TryLoad(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvml.dll"), out library))
        {
            return null;
        }
        try
        {
            return new NvmlLibrary(library, gpu);
        }
        catch (EntryPointNotFoundException)
        {
            NativeLibrary.Free(library);
            return null;
        }
    }

    /// <summary>The exported function <paramref name="name"/>, or 0 on drivers without it.</summary>
    public nint Export(string name) => NativeLibrary.TryGetExport(_library, name, out var address) ? address : 0;

    /// <summary>The GPU's handle, initialising NVML first if needed; false when the driver doesn't answer.</summary>
    public bool TryGetDevice(out nint device)
    {
        device = 0;
        if (!_initialized)
        {
            if (_init() != Success)
                return false;
            _initialized = true;
        }
        if (_device == 0)
            _device = FindDevice();
        device = _device;
        return device != 0;
    }

    /// <summary>Drops the handle after a failed call (e.g. after a driver reset); the next use looks it up again.</summary>
    public void ForgetDevice() => _device = 0;

    /// <summary>The driver's version, e.g. "610.62"; null before NVML has been initialised.</summary>
    public string? DriverVersion()
    {
        if (!_initialized || _driverVersion == null)
            return null;
        var buffer = stackalloc byte[96];
        return _driverVersion(buffer, 96) == Success ? Marshal.PtrToStringAnsi((nint)buffer) : null;
    }

    /// <summary>The discrete GPU DXGI found, by its PCI ids; without one (or a match), the first NVIDIA GPU.</summary>
    private nint FindDevice()
    {
        uint count;
        if (_deviceCount(&count) != Success || count == 0)
            return 0;
        nint device;
        if (_gpu is { } gpu && _pciInfo != null)
        {
            var id = gpu.DeviceId << 16 | gpu.VendorId;
            for (uint i = 0; i < count; i++)
            {
                PciInfo pci;
                if (_deviceByIndex(i, &device) == Success && _pciInfo(device, &pci) == Success &&
                    pci.PciDeviceId == id && pci.PciSubSystemId == gpu.SubsystemId)
                {
                    return device;
                }
            }
        }
        return _deviceByIndex(0, &device) == Success ? device : 0;
    }

    public void Dispose()
    {
        if (_initialized)
            _shutdown();
        _initialized = false;
        NativeLibrary.Free(_library);
    }
}
