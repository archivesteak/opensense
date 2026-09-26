using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Power;

namespace OpenSense.Core.Hardware;

/// <summary>How worn the battery is, as it reports itself to Windows (the ACPI battery's <c>_BIX</c>).</summary>
/// <param name="DesignCapacity">What it held when new: mWh, or the battery's own units when <paramref name="Relative"/>.</param>
/// <param name="FullChargeCapacity">What it holds when full now, in the same units.</param>
/// <param name="CycleCount">Charge cycles so far; null when the battery doesn't count them.</param>
/// <param name="Relative">The capacities are in units of the battery's own, not mWh.</param>
public sealed record BatteryHealth(int DesignCapacity, int FullChargeCapacity, int? CycleCount, bool Relative)
{
    public string? Manufacturer { get; init; }

    /// <summary>The battery's model, e.g. "AP18E8M".</summary>
    public string? Name { get; init; }

    /// <summary>The full-charge capacity as a share of the design capacity (over 100 is possible on a new battery).</summary>
    public int Percent => DesignCapacity > 0 ? (int)Math.Round(100.0 * FullChargeCapacity / DesignCapacity) : 0;
}

/// <summary>The laptop's battery through Windows' battery class driver; needs no administrator rights.</summary>
public static unsafe class WindowsBattery
{
    private enum InformationLevel
    {
        Information = 0,
        DeviceName = 4,
        ManufactureName = 6,
    }

    /// <summary>The first battery that answers; null without one.</summary>
    public static BatteryHealth? Read()
    {
        foreach (var path in BatteryPaths())
        {
            using var handle = PInvoke.CreateFile(path, (uint)(GENERIC_ACCESS_RIGHTS.GENERIC_READ | GENERIC_ACCESS_RIGHTS.GENERIC_WRITE),
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE, null, FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL, null);
            if (!handle.IsInvalid && Describe((HANDLE)handle.DangerousGetHandle()) is { } health)
                return health;
        }
        return null;
    }

    private static BatteryHealth? Describe(HANDLE handle)
    {
        // The tag names the battery now in the slot; queries about another one fail.
        uint wait = 0, tag = 0;
        if (!PInvoke.DeviceIoControl(handle, PInvoke.IOCTL_BATTERY_QUERY_TAG, &wait, sizeof(uint), &tag, sizeof(uint), null, null) || tag == 0)
            return null;

        BATTERY_INFORMATION info;
        if (!Query(handle, tag, InformationLevel.Information, &info, (uint)sizeof(BATTERY_INFORMATION)) || info.DesignedCapacity == 0)
            return null;
        return new BatteryHealth(
            (int)Math.Min(info.DesignedCapacity, int.MaxValue),
            (int)Math.Min(info.FullChargedCapacity, int.MaxValue),
            info.CycleCount is > 0 and < int.MaxValue ? (int)info.CycleCount : null,
            (info.Capabilities & PInvoke.BATTERY_CAPACITY_RELATIVE) != 0)
        {
            Manufacturer = Text(handle, tag, InformationLevel.ManufactureName),
            Name = Text(handle, tag, InformationLevel.DeviceName),
        };
    }

    private static bool Query(HANDLE handle, uint tag, InformationLevel level, void* output, uint size)
    {
        var query = new BATTERY_QUERY_INFORMATION { BatteryTag = tag, InformationLevel = (BATTERY_QUERY_INFORMATION_LEVEL)level };
        uint returned = 0;
        return PInvoke.DeviceIoControl(handle, PInvoke.IOCTL_BATTERY_QUERY_INFORMATION, &query, (uint)sizeof(BATTERY_QUERY_INFORMATION),
            output, size, &returned, null) && returned > 0;
    }

    private static string? Text(HANDLE handle, uint tag, InformationLevel level)
    {
        const int Length = 128;
        var buffer = stackalloc char[Length];
        if (!Query(handle, tag, level, buffer, Length * sizeof(char)))
            return null;
        var text = new string(buffer, 0, Length).Split('\0')[0].Trim();
        return text.Length > 0 ? text : null;
    }

    private static string[] BatteryPaths()
    {
        var battery = PInvoke.GUID_DEVICE_BATTERY;
        const CM_GET_DEVICE_INTERFACE_LIST_FLAGS present = CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT;
        while (true)
        {
            if (PInvoke.CM_Get_Device_Interface_List_Size(out var length, battery, null, present) != CONFIGRET.CR_SUCCESS)
                return [];
            var buffer = new char[length];
            CONFIGRET result;
            fixed (char* list = buffer)
                result = PInvoke.CM_Get_Device_Interface_List(battery, null, new PZZWSTR(list), length, present);
            if (result == CONFIGRET.CR_SUCCESS)
                return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (result != CONFIGRET.CR_BUFFER_SMALL) // a battery arrived in between: ask again
                return [];
        }
    }
}
