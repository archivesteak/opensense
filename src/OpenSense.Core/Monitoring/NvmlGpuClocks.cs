using System.Runtime.InteropServices;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// The NVIDIA GPU's clock offsets through NVML: <c>nvmlDeviceGet/SetClockOffsets</c> for P0, the performance state
/// games run in (drivers from 555 on), else the older <c>nvmlDevice{Get,Set}{Gpc,Mem}ClkVfOffset</c>. The driver
/// keeps them until it restarts. Setting needs administrator rights.
/// </summary>
internal sealed unsafe class NvmlGpuClocks : IGpuClockControl
{
    private const int GraphicsClock = 0, MemoryClock = 2; // nvmlClockType_t
    private const int PState0 = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct ClockOffset // nvmlClockOffset_v1_t
    {
        public uint Version;
        public int Type;
        public int PState;
        public int Offset;
        public int Min;
        public int Max;
    }

    private readonly NvmlLibrary _nvml;
    private readonly delegate* unmanaged<nint, ClockOffset*, int> _getOffsets;
    private readonly delegate* unmanaged<nint, ClockOffset*, int> _setOffsets;
    private readonly delegate* unmanaged<nint, int*, int> _getCore;
    private readonly delegate* unmanaged<nint, int*, int> _getMemory;
    private readonly delegate* unmanaged<nint, int*, int*, int> _coreRange;
    private readonly delegate* unmanaged<nint, int*, int*, int> _memoryRange;
    private readonly delegate* unmanaged<nint, int, int> _setCore;
    private readonly delegate* unmanaged<nint, int, int> _setMemory;
    private bool _legacy; // the older calls answered where the newer ones don't

    private NvmlGpuClocks(NvmlLibrary nvml, string device)
    {
        _nvml = nvml;
        Device = device;
        _getOffsets = (delegate* unmanaged<nint, ClockOffset*, int>)nvml.Export("nvmlDeviceGetClockOffsets");
        _setOffsets = (delegate* unmanaged<nint, ClockOffset*, int>)nvml.Export("nvmlDeviceSetClockOffsets");
        _getCore = (delegate* unmanaged<nint, int*, int>)nvml.Export("nvmlDeviceGetGpcClkVfOffset");
        _getMemory = (delegate* unmanaged<nint, int*, int>)nvml.Export("nvmlDeviceGetMemClkVfOffset");
        _coreRange = (delegate* unmanaged<nint, int*, int*, int>)nvml.Export("nvmlDeviceGetGpcClkMinMaxVfOffset");
        _memoryRange = (delegate* unmanaged<nint, int*, int*, int>)nvml.Export("nvmlDeviceGetMemClkMinMaxVfOffset");
        _setCore = (delegate* unmanaged<nint, int, int>)nvml.Export("nvmlDeviceSetGpcClkVfOffset");
        _setMemory = (delegate* unmanaged<nint, int, int>)nvml.Export("nvmlDeviceSetMemClkVfOffset");
    }

    /// <summary>The control, on a driver that has either set of calls; <paramref name="device"/> names the GPU.</summary>
    public static NvmlGpuClocks? TryOpen(NvmlLibrary nvml, string device)
    {
        var clocks = new NvmlGpuClocks(nvml, device);
        return clocks._getOffsets != null || (clocks._getCore != null && clocks._coreRange != null) ? clocks : null;
    }

    public string Device { get; }

    public string? DriverVersion { get; private set; }

    public bool Unsupported { get; private set; }

    public GpuClockReading? Read()
    {
        if (!_nvml.TryGetDevice(out var device))
            return null;
        DriverVersion ??= _nvml.DriverVersion();

        GpuClockReading? reading = null;
        var result = _getOffsets != null ? ReadCurrent(device, out reading) : NvmlLibrary.NotSupported;
        if (result == NvmlLibrary.NotSupported)
            result = ReadLegacy(device, out reading);
        if (result == NvmlLibrary.Success)
        {
            Unsupported = false;
            return reading;
        }
        if (result == NvmlLibrary.NotSupported)
            Unsupported = true;
        else
            _nvml.ForgetDevice();
        return null;
    }

    public bool Write(GpuClock clock, int offsetMhz)
    {
        if (!_nvml.TryGetDevice(out var device))
            return false;
        var type = clock == GpuClock.Core ? GraphicsClock : MemoryClock;
        if (!_legacy && _setOffsets != null)
        {
            var info = Offset(type, offsetMhz);
            return _setOffsets(device, &info) == NvmlLibrary.Success;
        }
        var set = clock == GpuClock.Core ? _setCore : _setMemory;
        return set != null && set(device, offsetMhz) == NvmlLibrary.Success;
    }

    private int ReadCurrent(nint device, out GpuClockReading? reading)
    {
        reading = null;
        var core = Offset(GraphicsClock);
        var result = _getOffsets(device, &core);
        if (result != NvmlLibrary.Success)
            return result;
        // A GPU whose memory clock takes no offset keeps it at 0.
        var memory = Offset(MemoryClock);
        if (_getOffsets(device, &memory) != NvmlLibrary.Success)
            memory = default;
        _legacy = false;
        reading = new GpuClockReading(new ClockOffsets(core.Offset, memory.Offset), Limits(core.Min, core.Max, memory.Min, memory.Max));
        return result;
    }

    private int ReadLegacy(nint device, out GpuClockReading? reading)
    {
        reading = null;
        if (_getCore == null || _coreRange == null)
            return NvmlLibrary.NotSupported;
        int core = 0, coreMin = 0, coreMax = 0;
        var result = _getCore(device, &core);
        if (result == NvmlLibrary.Success)
            result = _coreRange(device, &coreMin, &coreMax);
        if (result != NvmlLibrary.Success)
            return result;
        int memory = 0, memoryMin = 0, memoryMax = 0;
        if (_getMemory == null || _memoryRange == null || _getMemory(device, &memory) != NvmlLibrary.Success ||
            _memoryRange(device, &memoryMin, &memoryMax) != NvmlLibrary.Success)
        {
            memory = memoryMin = memoryMax = 0;
        }
        _legacy = true;
        reading = new GpuClockReading(new ClockOffsets(core, memory), Limits(coreMin, coreMax, memoryMin, memoryMax));
        return result;
    }

    private static ClockOffset Offset(int type, int offset = 0) =>
        new() { Version = (uint)sizeof(ClockOffset) | (1u << 24), Type = type, PState = PState0, Offset = offset };

    /// <summary>A range the driver reports upside down counts as none.</summary>
    private static GpuClockLimits Limits(int coreMin, int coreMax, int memoryMin, int memoryMax) =>
        new(coreMin <= coreMax ? coreMin : 0, coreMin <= coreMax ? coreMax : 0,
            memoryMin <= memoryMax ? memoryMin : 0, memoryMin <= memoryMax ? memoryMax : 0);
}
