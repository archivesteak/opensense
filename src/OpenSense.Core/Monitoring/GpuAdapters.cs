using Windows.Win32;
using Windows.Win32.Graphics.Dxgi;

namespace OpenSense.Core.Monitoring;

public sealed record GpuAdapter(string Name, uint VendorId, ulong DedicatedMemory, uint LuidLow, int LuidHigh)
{
    /// <summary>The LUID as it appears in "GPU Engine" counter instance names.</summary>
    public string CounterLuid => $"luid_0x{LuidHigh:X8}_0x{LuidLow:X8}";
}

/// <summary>Enumerates hardware GPUs through DXGI (no Direct3D device is created).</summary>
public static class GpuAdapters
{
    private const uint MicrosoftBasicRenderVendorId = 0x1414;

    public static unsafe IReadOnlyList<GpuAdapter> Enumerate()
    {
        var result = new List<GpuAdapter>();
        var iid = typeof(IDXGIFactory1).GUID;
        if (PInvoke.CreateDXGIFactory1(&iid, out var factoryObject).Failed)
            return result;

        var factory = (IDXGIFactory1)factoryObject;
        for (uint i = 0; ; i++)
        {
            var hr = factory.EnumAdapters1(i, out var adapter);
            if (hr == Windows.Win32.Foundation.HRESULT.DXGI_ERROR_NOT_FOUND || hr.Failed)
                break;

            var desc = adapter.GetDesc1();
            var software = ((DXGI_ADAPTER_FLAG)desc.Flags).HasFlag(DXGI_ADAPTER_FLAG.DXGI_ADAPTER_FLAG_SOFTWARE);
            if (!software && desc.VendorId != MicrosoftBasicRenderVendorId)
            {
                result.Add(new GpuAdapter(desc.Description.ToString(), desc.VendorId, desc.DedicatedVideoMemory,
                    desc.AdapterLuid.LowPart, desc.AdapterLuid.HighPart));
            }
        }
        return result;
    }

    /// <summary>The discrete GPU: the hardware adapter with the most dedicated memory.</summary>
    public static GpuAdapter? FindDiscrete(IReadOnlyList<GpuAdapter> adapters) =>
        adapters.OrderByDescending(a => a.DedicatedMemory).FirstOrDefault();
}
