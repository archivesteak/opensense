using System.Globalization;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>A value in a report, as the report descriptor declares it.</summary>
/// <param name="BitSize">Bits per value.</param>
/// <param name="ReportCount">How many values of that size.</param>
public sealed record HidValueField(byte ReportId, ushort UsagePage, ushort Usage, int BitSize, int ReportCount);

/// <summary>A HID interface (one top-level collection) and what its report descriptor says about it.</summary>
/// <param name="InputLength">Report lengths in bytes, including the report id byte (0 when there are none).</param>
public sealed record HidDeviceInfo(
    string Path, ushort VendorId, ushort ProductId, ushort Version, ushort UsagePage, ushort Usage,
    int InputLength, int OutputLength, int FeatureLength)
{
    /// <summary>The values its feature reports carry.</summary>
    public IReadOnlyList<HidValueField> FeatureValues { get; init; } = [];

    /// <summary>The declared value <paramref name="usage"/> in feature report <paramref name="reportId"/>; null when there is none.</summary>
    public HidValueField? FeatureValue(byte reportId, ushort usagePage, ushort usage) =>
        FeatureValues.FirstOrDefault(v => v.ReportId == reportId && v.UsagePage == usagePage && v.Usage == usage);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{VendorId:X4}:{ProductId:X4} rev {Version:X4} usage page {UsagePage:X4} usage {Usage:X4} " +
        $"reports in {InputLength} out {OutputLength} feature {FeatureLength}");
}

/// <summary>The machine's HID interfaces.</summary>
public interface IHidBus
{
    /// <summary>The HID interfaces present now.</summary>
    IReadOnlyList<HidDeviceInfo> Enumerate();

    /// <summary>Opens an interface for reports; null when it is gone or held exclusively.</summary>
    IHidDevice? Open(HidDeviceInfo device);

    /// <summary>Raised (on a worker thread) when a HID interface arrives or leaves, with its path.</summary>
    event Action<string>? Changed
    {
        add { }
        remove { }
    }
}

/// <summary>An open HID interface. Calls are synchronous and not thread-safe.</summary>
public interface IHidDevice : IDisposable
{
    HidDeviceInfo Info { get; }

    /// <summary>Reads a feature report; <c>report[0]</c> is the report id and the rest is filled in.</summary>
    bool GetFeature(Span<byte> report);

    /// <summary>Sends a feature report; <c>report[0]</c> is the report id.</summary>
    bool SetFeature(ReadOnlySpan<byte> report);

    /// <summary>Sends an output report on the interrupt pipe; <c>report[0]</c> is the report id.</summary>
    bool Write(ReadOnlySpan<byte> report);
}

/// <summary>HID interfaces through Windows' HID class driver.</summary>
public sealed unsafe class WindowsHidBus : IHidBus, IDisposable
{
    private readonly object _gate = new();
    private Action<string>? _changed;
    private PCM_NOTIFY_CALLBACK? _callback; // kept alive while registered
    private CM_Unregister_NotificationSafeHandle? _registration;

    /// <summary>
    /// Arrivals and removals of HID interfaces, through the configuration manager's notifications (a service gets no
    /// window messages). Registered with the first handler.
    /// </summary>
    public event Action<string>? Changed
    {
        add
        {
            lock (_gate)
            {
                _changed += value;
                if (_registration is null)
                    Register();
            }
        }
        remove
        {
            lock (_gate)
                _changed -= value;
        }
    }

    public IReadOnlyList<HidDeviceInfo> Enumerate()
    {
        PInvoke.HidD_GetHidGuid(out var hidClass);
        const CM_GET_DEVICE_INTERFACE_LIST_FLAGS present = CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT;
        char[] buffer;
        while (true)
        {
            if (PInvoke.CM_Get_Device_Interface_List_Size(out var length, hidClass, null, present) != CONFIGRET.CR_SUCCESS)
                return [];
            buffer = new char[length];
            CONFIGRET result;
            fixed (char* list = buffer)
                result = PInvoke.CM_Get_Device_Interface_List(hidClass, null, new PZZWSTR(list), length, present);
            if (result == CONFIGRET.CR_SUCCESS)
                break;
            if (result != CONFIGRET.CR_BUFFER_SMALL) // a device arrived in between: ask again
                return [];
        }

        var devices = new List<HidDeviceInfo>();
        foreach (var path in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // No access rights needed to read the attributes, so this works for keyboards Windows holds too.
            using var handle = OpenPath(path, 0);
            if (!handle.IsInvalid && Describe(path, handle) is { } info)
                devices.Add(info);
        }
        return devices;
    }

    public IHidDevice? Open(HidDeviceInfo device)
    {
        var handle = OpenPath(device.Path, (uint)(GENERIC_ACCESS_RIGHTS.GENERIC_READ | GENERIC_ACCESS_RIGHTS.GENERIC_WRITE));
        if (!handle.IsInvalid)
            return new WindowsHidDevice(device, handle);
        handle.Dispose();
        return null;
    }

    private static SafeFileHandle OpenPath(string path, uint access) => PInvoke.CreateFile(path, access,
        FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE, null, FILE_CREATION_DISPOSITION.OPEN_EXISTING,
        FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL, null);

    private static HidDeviceInfo? Describe(string path, SafeFileHandle handle)
    {
        if (!PInvoke.HidD_GetAttributes(handle, out var attributes) || !PInvoke.HidD_GetPreparsedData(handle, out var preparsed))
            return null;
        try
        {
            if (PInvoke.HidP_GetCaps(preparsed, out var caps) != NTSTATUS.HIDP_STATUS_SUCCESS)
                return null;
            return new HidDeviceInfo(path, attributes.VendorID, attributes.ProductID, attributes.VersionNumber,
                caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength)
            {
                FeatureValues = FeatureValues(preparsed, caps.NumberFeatureValueCaps),
            };
        }
        finally
        {
            PInvoke.HidD_FreePreparsedData(preparsed);
        }
    }

    /// <summary>The feature reports' values; a usage range becomes one field per usage.</summary>
    private static HidValueField[] FeatureValues(PHIDP_PREPARSED_DATA preparsed, ushort count)
    {
        if (count == 0)
            return [];
        var caps = new HIDP_VALUE_CAPS[count];
        if (PInvoke.HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Feature, caps, ref count, preparsed) != NTSTATUS.HIDP_STATUS_SUCCESS)
            return [];
        List<HidValueField> fields = [];
        foreach (var cap in caps.AsSpan(0, count))
        {
            if (cap.IsRange)
            {
                for (var usage = cap.Anonymous.Range.UsageMin; usage <= cap.Anonymous.Range.UsageMax && usage != 0; usage++)
                    fields.Add(new HidValueField(cap.ReportID, cap.UsagePage, usage, cap.BitSize, 1));
            }
            else
            {
                fields.Add(new HidValueField(cap.ReportID, cap.UsagePage, cap.Anonymous.NotRange.Usage, cap.BitSize, cap.ReportCount));
            }
        }
        return [.. fields];
    }

    private void Register()
    {
        PInvoke.HidD_GetHidGuid(out var hidClass);
        var filter = new CM_NOTIFY_FILTER
        {
            cbSize = (uint)sizeof(CM_NOTIFY_FILTER),
            FilterType = CM_NOTIFY_FILTER_TYPE.CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE,
        };
        filter.u.DeviceInterface.ClassGuid = hidClass;
        _callback = OnNotification;
        if (PInvoke.CM_Register_Notification(filter, null, _callback, out var registration) == CONFIGRET.CR_SUCCESS)
        {
            _registration = registration;
        }
        else
        {
            registration.Dispose();
            _callback = null;
        }
    }

    /// <summary>Hands the news to the thread pool: the configuration manager's thread must not wait on anything.</summary>
    private uint OnNotification(HCMNOTIFICATION notification, void* context, CM_NOTIFY_ACTION action, CM_NOTIFY_EVENT_DATA* data, uint size)
    {
        if (action is CM_NOTIFY_ACTION.CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL or CM_NOTIFY_ACTION.CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL
            && data is not null)
        {
            // The interface's path follows the class GUID, NUL-terminated.
            var path = new string((char*)&data->u.DeviceInterface.SymbolicLink);
            ThreadPool.QueueUserWorkItem(_ => _changed?.Invoke(path));
        }
        return 0;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _changed = null;
            _registration?.Dispose(); // unregisters, and waits for a callback in progress
            _registration = null;
            _callback = null;
        }
    }
}

internal sealed class WindowsHidDevice(HidDeviceInfo info, SafeFileHandle handle) : IHidDevice
{
    public HidDeviceInfo Info { get; } = info;

    public bool GetFeature(Span<byte> report) => PInvoke.HidD_GetFeature(handle, report);

    public bool SetFeature(ReadOnlySpan<byte> report) => PInvoke.HidD_SetFeature(handle, report);

    public bool Write(ReadOnlySpan<byte> report)
    {
        var overlapped = default(NativeOverlapped);
        return PInvoke.WriteFile(handle, report, out var written, ref overlapped) && written == report.Length;
    }

    public void Dispose() => handle.Dispose();
}
