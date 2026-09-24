using Windows.Win32;
using Windows.Win32.Graphics.Dxgi;

namespace OpenSense.Core.Monitoring;

public sealed record GpuAdapter(string Name, uint VendorId, ulong DedicatedMemory, uint LuidLow, int LuidHigh,
    uint DeviceId = 0, uint SubsystemId = 0, uint Revision = 0)
{
    public const uint NvidiaVendorId = 0x10DE;

    /// <summary>The LUID as it appears in "GPU Engine" counter instance names.</summary>
    public string CounterLuid => $"luid_0x{LuidHigh:X8}_0x{LuidLow:X8}";

    /// <summary>The PCI hardware ID its device instance ID starts with, e.g. <c>PCI\VEN_10DE&amp;DEV_249D&amp;SUBSYS_153A1025&amp;REV_A1</c>.</summary>
    public string PnpHardwareId => $@"PCI\VEN_{VendorId:X4}&DEV_{DeviceId:X4}&SUBSYS_{SubsystemId:X8}&REV_{Revision:X2}";
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
                    desc.AdapterLuid.LowPart, desc.AdapterLuid.HighPart, desc.DeviceId, desc.SubSysId, desc.Revision));
            }
        }
        return result;
    }

    /// <summary>Like <see cref="Enumerate"/>, but empty when DXGI fails.</summary>
    public static IReadOnlyList<GpuAdapter> TryEnumerate()
    {
        try
        {
            return Enumerate();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>The discrete GPU: the hardware adapter with the most dedicated memory.</summary>
    public static GpuAdapter? FindDiscrete(IReadOnlyList<GpuAdapter> adapters) =>
        adapters.OrderByDescending(a => a.DedicatedMemory).FirstOrDefault();
}
