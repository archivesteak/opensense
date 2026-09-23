using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace OpenSense.Core.Monitoring;

public interface ITemperatureSensor : IDisposable
{
    /// <summary>What is being read, for the UI (e.g. "Intel package sensor").</summary>
    string Description { get; }

    /// <summary>Degrees Celsius, or null when the reading failed.</summary>
    double? Read();
}

/// <summary>
/// The CPU's own digital thermal sensor, read through PawnIO: the same registers ThrottleStop, HWiNFO and
/// Core Temp use. Updates instantly and to the degree, unlike the embedded controller's copy.
/// </summary>
internal static class CpuTemperatureSensor
{
    /// <summary>Opens the sensor, or returns null and logs why (no PawnIO, unsupported CPU, ...).</summary>
    public static ITemperatureSensor? TryOpen(Action<string>? log = null)
    {
        if (!X86Base.IsSupported)
        {
            log?.Invoke("Not an x86 CPU.");
            return null;
        }
        var vendor = Vendor();
        switch (vendor)
        {
            case "GenuineIntel":
                return IntelPackageSensor.TryOpen(log);
            case "AuthenticAMD":
                return AmdTctlSensor.TryOpen(log);
            default:
                log?.Invoke($"Unsupported CPU vendor {vendor}.");
                return null;
        }
    }

    private static string Vendor()
    {
        var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
        ReadOnlySpan<int> parts = [ebx, edx, ecx];
        return Encoding.ASCII.GetString(MemoryMarshal.AsBytes(parts));
    }
}

/// <summary>Intel: package temperature = TjMax − the IA32_PACKAGE_THERM_STATUS digital readout.</summary>
internal sealed class IntelPackageSensor : ITemperatureSensor
{
    private const ulong TemperatureTarget = 0x1A2;  // MSR_TEMPERATURE_TARGET; bits 23:16 = TjMax
    private const ulong PackageThermStatus = 0x1B1; // IA32_PACKAGE_THERM_STATUS; bits 22:16 = degrees below TjMax

    private readonly PawnIOModule _module;
    private readonly int _tjMax;

    private IntelPackageSensor(PawnIOModule module, int tjMax)
    {
        _module = module;
        _tjMax = tjMax;
    }

    public string Description => $"Intel package sensor (TjMax {_tjMax} °C)";

    public static ITemperatureSensor? TryOpen(Action<string>? log)
    {
        // CPUID.06H:EAX[6]: package thermal management, i.e. IA32_PACKAGE_THERM_STATUS exists.
        if ((X86Base.CpuId(6, 0).Eax & (1 << 6)) == 0)
        {
            log?.Invoke("This Intel CPU has no package temperature sensor.");
            return null;
        }
        if (PawnIOModule.TryOpen("IntelMSR", log) is not { } module)
            return null;

        var tjMax = module.Call("ioctl_read_msr", TemperatureTarget) is { } target ? (int)((target >> 16) & 0xFF) : 0;
        if (tjMax is < 60 or > 130)
        {
            module.Dispose();
            log?.Invoke($"The CPU reported an implausible TjMax ({tjMax} °C).");
            return null;
        }
        var sensor = new IntelPackageSensor(module, tjMax);
        if (sensor.Read() is null)
        {
            sensor.Dispose();
            log?.Invoke("The CPU package sensor did not answer.");
            return null;
        }
        return sensor;
    }

    public double? Read()
    {
        if (_module.Call("ioctl_read_msr", PackageThermStatus) is not { } status)
            return null;
        var celsius = _tjMax - (int)((status >> 16) & 0x7F);
        return celsius > 0 ? celsius : null;
    }

    public void Dispose() => _module.Dispose();
}

/// <summary>
/// AMD Zen (families 17h–1Ah): Tctl from the SMU's THM_TCON_CUR_TMP register, decoded like Linux k10temp.
/// Ryzen laptop chips report Tctl = Tdie (the +10/+20/+27 °C offsets only exist on some desktop parts).
/// </summary>
internal sealed class AmdTctlSensor : ITemperatureSensor
{
    private const ulong ThmTconCurTmp = 0x00059800;

    private readonly PawnIOModule _module;
    private readonly Mutex _pciLock;

    private AmdTctlSensor(PawnIOModule module, Mutex pciLock)
    {
        _module = module;
        _pciLock = pciLock;
    }

    public string Description => "AMD Tctl sensor";

    public static ITemperatureSensor? TryOpen(Action<string>? log)
    {
        // The module itself refuses to load outside families 17h–1Ah.
        if (PawnIOModule.TryOpen("AMDFamily17", log) is not { } module)
            return null;

        Mutex pciLock;
        try
        {
            // SMN reads go through a shared PCI index/data pair; monitoring tools serialise on this mutex.
            pciLock = new Mutex(false, @"Global\Access_PCI");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            module.Dispose();
            log?.Invoke($"Could not open the shared PCI lock: {ex.Message}");
            return null;
        }

        var sensor = new AmdTctlSensor(module, pciLock);
        if (sensor.Read() is null)
        {
            sensor.Dispose();
            log?.Invoke("The CPU temperature register did not answer.");
            return null;
        }
        return sensor;
    }

    public double? Read()
    {
        bool locked;
        try
        {
            locked = _pciLock.WaitOne(TimeSpan.FromMilliseconds(100));
        }
        catch (AbandonedMutexException)
        {
            locked = true; // the previous owner died; the mutex is ours now
        }
        if (!locked)
            return null;

        try
        {
            if (_module.Call("ioctl_read_smn", ThmTconCurTmp) is not { } value)
                return null;
            var raw = (uint)value;
            var celsius = (raw >> 21) * 0.125;
            // Range select (bit 19), or both TJ select bits (17:16): the value carries a 49 °C offset.
            if ((raw & (1u << 19)) != 0 || (raw & (3u << 16)) == 3u << 16)
                celsius -= 49;
            return celsius > 0 ? celsius : null;
        }
        finally
        {
            _pciLock.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        _module.Dispose();
        _pciLock.Dispose();
    }
}
