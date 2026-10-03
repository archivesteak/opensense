using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace OpenSense.Core.Monitoring;

public interface ITemperatureSensor : IDisposable
{
    /// <summary>What is being read.</summary>
    SensorStatus Status { get; }

    /// <summary>Degrees Celsius, or null when the reading failed.</summary>
    double? Read();

    /// <summary>
    /// Where the chip starts slowing itself down because it is hot (°C), as it reports it, or null when it doesn't (or
    /// hasn't been asked yet). Kept up to date by <see cref="Read"/>.
    /// </summary>
    int? Limit => null;

    /// <summary>
    /// What the log should hear about a reading the chip returned that cannot be a temperature (the caller discards it), or
    /// null when there is nothing new. Each note is handed out once, on the thread that reads.
    /// </summary>
    string? TakeGlitchNote() => null;
}

/// <summary>
/// The CPU's own digital thermal sensor, read through PawnIO: the same registers ThrottleStop, HWiNFO and
/// Core Temp use. Updates instantly and to the degree, unlike the embedded controller's copy.
/// </summary>
internal static class CpuTemperatureSensor
{
    /// <summary>Opens the sensor, or returns null and reports why (no PawnIO, unsupported CPU, ...).</summary>
    public static ITemperatureSensor? TryOpen(Action<SensorProblem, string?>? fail = null)
    {
        if (!X86Base.IsSupported)
        {
            fail?.Invoke(SensorProblem.NotX86Cpu, null);
            return null;
        }
        var vendor = Vendor();
        switch (vendor)
        {
            case "GenuineIntel":
                return IntelPackageSensor.TryOpen(fail);
            case "AuthenticAMD":
                return AmdTctlSensor.TryOpen(fail);
            default:
                fail?.Invoke(SensorProblem.UnsupportedCpuVendor, vendor);
                return null;
        }
    }

    /// <summary>The CPU's own limit where it doesn't report one (<see cref="ITemperatureSensor.Limit"/>).</summary>
    public static int DefaultLimit() =>
        X86Base.IsSupported && Vendor() == "AuthenticAMD" ? ThermalLimits.AmdCpuDefault : ThermalLimits.IntelCpuDefault;

    private static string Vendor()
    {
        var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
        ReadOnlySpan<int> parts = [ebx, edx, ecx];
        return Encoding.ASCII.GetString(MemoryMarshal.AsBytes(parts));
    }
}

/// <summary>
/// Intel: package temperature = TjMax − the IA32_PACKAGE_THERM_STATUS digital readout. The CPU slows down at TjMax less
/// its TCC offset, which Intel's Dynamic Tuning sets per profile on some Acer boards (8 °C in their Quiet and Default
/// profiles, none in Extreme and Turbo), so the offset is read along with every temperature.
/// </summary>
internal sealed class IntelPackageSensor : ITemperatureSensor
{
    private const ulong TemperatureTarget = 0x1A2;  // MSR_TEMPERATURE_TARGET; bits 23:16 = TjMax, 29:24 = TCC offset
    private const ulong PackageThermStatus = 0x1B1; // IA32_PACKAGE_THERM_STATUS; bits 22:16 = degrees below TjMax

    private readonly PawnIOModule _module;
    private readonly int _tjMax;

    private IntelPackageSensor(PawnIOModule module, int tjMax)
    {
        _module = module;
        _tjMax = tjMax;
    }

    public SensorStatus Status => new(ChipSensor.IntelPackage, _tjMax);

    public int? Limit { get; private set; }

    public static ITemperatureSensor? TryOpen(Action<SensorProblem, string?>? fail)
    {
        // CPUID.06H:EAX[6]: package thermal management, i.e. IA32_PACKAGE_THERM_STATUS exists.
        if ((X86Base.CpuId(6, 0).Eax & (1 << 6)) == 0)
        {
            fail?.Invoke(SensorProblem.NoPackageSensor, null);
            return null;
        }
        if (PawnIOModule.TryOpen("IntelMSR", fail) is not { } module)
            return null;

        var tjMax = module.Call("ioctl_read_msr", TemperatureTarget) is { } target ? (int)((target >> 16) & 0xFF) : 0;
        if (tjMax is < 60 or > 130)
        {
            module.Dispose();
            fail?.Invoke(SensorProblem.ImplausibleTjMax, tjMax.ToString(CultureInfo.InvariantCulture));
            return null;
        }
        var sensor = new IntelPackageSensor(module, tjMax);
        if (sensor.Read() is null)
        {
            sensor.Dispose();
            fail?.Invoke(SensorProblem.NoReading, null);
            return null;
        }
        return sensor;
    }

    public double? Read()
    {
        if (_module.Call("ioctl_read_msr", PackageThermStatus) is not { } status)
            return null;
        if (_module.Call("ioctl_read_msr", TemperatureTarget) is { } target)
            Limit = _tjMax - (int)((target >> 24) & 0x3F);
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

    // Readings that cannot be temperatures are counted and told to the log (TakeGlitchNote): the first ones in full, then
    // one in GlitchTellEvery, and always a raw value not seen before.
    private const int GlitchesTold = 20;
    private const int GlitchTellEvery = 50;
    private const int GlitchValuesKept = 16;

    private readonly PawnIOModule _module;
    private readonly Mutex _pciLock;
    private readonly HashSet<uint> _glitchValues = [];
    private long _reads, _glitches;
    private long? _lastGlitchAt;
    private string? _glitchNote;

    private AmdTctlSensor(PawnIOModule module, Mutex pciLock)
    {
        _module = module;
        _pciLock = pciLock;
    }

    public SensorStatus Status { get; } = new(ChipSensor.AmdTctl);

    public static ITemperatureSensor? TryOpen(Action<SensorProblem, string?>? fail)
    {
        // The module itself refuses to load outside families 17h–1Ah.
        if (PawnIOModule.TryOpen("AMDFamily17", fail) is not { } module)
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
            fail?.Invoke(SensorProblem.PciLockUnavailable, ex.Message);
            return null;
        }

        var sensor = new AmdTctlSensor(module, pciLock);
        if (sensor.Read() is null)
        {
            sensor.Dispose();
            fail?.Invoke(SensorProblem.NoReading, null);
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
            if (ReadRegister() is not { } raw)
                return null;
            _reads++;
            var celsius = Decode(raw);
            if (celsius > 0 && SensorReadings.Temperature(celsius) is null)
                NoteGlitch(raw);
            return celsius > 0 ? celsius : null;
        }
        finally
        {
            _pciLock.ReleaseMutex();
        }
    }

    public string? TakeGlitchNote()
    {
        var note = _glitchNote;
        _glitchNote = null;
        return note;
    }

    private uint? ReadRegister() => _module.Call("ioctl_read_smn", ThmTconCurTmp) is { } value ? (uint)value : null;

    /// <summary>The register's temperature: eighths of a degree in bits 31:21.</summary>
    internal static double Decode(uint raw)
    {
        var celsius = (raw >> 21) * 0.125;
        // Range select (bit 19), or both TJ select bits (17:16): the value carries a 49 °C offset.
        if ((raw & (1u << 19)) != 0 || (raw & (3u << 16)) == 3u << 16)
            celsius -= 49;
        return celsius;
    }

    /// <summary>
    /// A bus read that gave back something that cannot be a temperature, e.g. all ones: 0xFFFFFFFF decodes to 206.9 °C.
    /// Called with the PCI lock held, from <see cref="Read"/>.
    /// </summary>
    private void NoteGlitch(uint raw)
    {
        _glitches++;
        var now = Environment.TickCount64;
        var after = _lastGlitchAt is { } last ? ((now - last) / 1000).ToString(CultureInfo.InvariantCulture) : null;
        _lastGlitchAt = now;
        var unseen = _glitchValues.Count < GlitchValuesKept && _glitchValues.Add(raw);
        if (_glitches > GlitchesTold && !unseen && _glitches % GlitchTellEvery != 0)
            return;

        // Asked again at once, is it still wrong? That tells a passing clash on the bus from a chip that isn't answering.
        var again = ReadRegister() is { } second ? Describe(second) : "no answer";
        var since = after is null ? "" : $", {after} s after the previous one";
        _glitchNote = string.Create(CultureInfo.InvariantCulture,
            $"Discarded {Describe(raw)}; read again at once: {again}; {_glitches} discarded of {_reads} readings{since}");
    }

    private static string Describe(uint raw) => string.Create(CultureInfo.InvariantCulture, $"0x{raw:X8} ({Decode(raw):0.#} °C)");

    public void Dispose()
    {
        _module.Dispose();
        _pciLock.Dispose();
    }
}
