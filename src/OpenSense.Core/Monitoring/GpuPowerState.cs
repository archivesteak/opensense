using Windows.Wdk.System.SystemServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;

namespace OpenSense.Core.Monitoring;

/// <summary>Whether the discrete GPU is powered, told without touching it.</summary>
public interface IGpuPowerState
{
    /// <summary>True while it is powered, false while it is powered down, null when this can't be told.</summary>
    bool? IsOn();
}

/// <summary>
/// The discrete GPU's power state as Windows records it (Device Manager's "Power data"): read from Windows' own
/// records of the device, never from the GPU, so it can't wake it. Windows only knows the GPU is off when it
/// powers it off itself, i.e. when the driver has enabled D3cold for it; older Optimus drivers cut the power on
/// their own while Windows goes on reporting D0. There this answers null rather than a D0 that may be wrong,
/// unless the GPU is the laptop's only one: then it runs the display, and nothing powers it off.
/// </summary>
internal sealed unsafe class WindowsGpuPowerState : IGpuPowerState
{
    private readonly string _instanceId;
    private readonly bool _onlyGpu;

    private WindowsGpuPowerState(string instanceId, bool onlyGpu)
    {
        _instanceId = instanceId;
        _onlyGpu = onlyGpu;
    }

    /// <summary>Finds <paramref name="gpu"/>'s display adapter device; null when there is none.</summary>
    public static IGpuPowerState? TryOpen(GpuAdapter gpu, bool onlyGpu)
    {
        var filter = PInvoke.GUID_DEVCLASS_DISPLAY.ToString("B");
        const uint flags = PInvoke.CM_GETIDLIST_FILTER_CLASS | PInvoke.CM_GETIDLIST_FILTER_PRESENT;
        if (PInvoke.CM_Get_Device_ID_List_Size(out var length, filter, flags) != CONFIGRET.CR_SUCCESS || length == 0)
            return null;
        var buffer = new char[length];
        fixed (char* list = buffer)
        {
            if (PInvoke.CM_Get_Device_ID_List(filter, new PZZWSTR(list), length, flags) != CONFIGRET.CR_SUCCESS)
                return null;
        }
        var id = new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(i => i.StartsWith(gpu.PnpHardwareId + @"\", StringComparison.OrdinalIgnoreCase));
        return id is null ? null : new WindowsGpuPowerState(id, onlyGpu);
    }

    public bool? IsOn()
    {
        uint node;
        fixed (char* id = _instanceId)
        {
            if (PInvoke.CM_Locate_DevNode(out node, new PWSTR(id), CM_LOCATE_DEVNODE_FLAGS.CM_LOCATE_DEVNODE_NORMAL) != CONFIGRET.CR_SUCCESS)
                return null;
        }
        if (!TryRead(node, PInvoke.DEVPKEY_Device_PowerData, out CM_POWER_DATA power))
            return null;
        switch (power.PD_MostRecentPowerState)
        {
            case DEVICE_POWER_STATE.PowerDeviceD0:
                return _onlyGpu || TryRead(node, PInvoke.DEVPKEY_PciDevice_D3ColdSupport, out uint d3Cold) &&
                    (d3Cold & (1u << (int)PCI_DEVICE_D3COLD_STATE_REASON.PciDeviceD3Cold_State_Enabled_BitIndex)) != 0
                    ? true
                    : null;
            case DEVICE_POWER_STATE.PowerDeviceD1 or DEVICE_POWER_STATE.PowerDeviceD2 or DEVICE_POWER_STATE.PowerDeviceD3:
                return false;
            default:
                return null;
        }
    }

    private static bool TryRead<T>(uint node, DEVPROPKEY key, out T value) where T : unmanaged
    {
        value = default;
        DEVPROPTYPE type;
        var size = (uint)sizeof(T);
        fixed (T* buffer = &value)
            return PInvoke.CM_Get_DevNode_Property(node, &key, &type, (byte*)buffer, &size, 0) == CONFIGRET.CR_SUCCESS && size == sizeof(T);
    }
}
