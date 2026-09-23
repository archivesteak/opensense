using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Engine;

/// <summary>Everything the engine reads the laptop through, so tests can put a simulated laptop behind it.</summary>
public interface IMachine
{
    /// <summary>E.g. "Nitro AN515-57"; null when Windows does not say.</summary>
    string? Model { get; }

    string? BiosVersion { get; }

    /// <summary>Acer's gaming WMI interface.</summary>
    IWmiTransport OpenFirmware();

    /// <summary>What NitroSense's installer left behind, used as detection hints.</summary>
    NitroSenseHints ReadHints();

    AcerSmbios ReadSmbios();

    ILoadMonitor OpenLoadMonitor();

    /// <summary>The CPU's and GPU's own temperature sensors, where available.</summary>
    DirectSensors OpenSensors();
}

/// <summary>This PC.</summary>
public sealed class WindowsMachine : IMachine
{
    public string? Model => SystemInfo.ReadModel();

    public string? BiosVersion => SystemInfo.ReadBiosVersion();

    public IWmiTransport OpenFirmware() => new WmiTransport();

    public NitroSenseHints ReadHints() => NitroSenseHints.Read();

    public AcerSmbios ReadSmbios() => AcerSmbios.Read();

    public ILoadMonitor OpenLoadMonitor() => new LoadMonitor();

    public DirectSensors OpenSensors() => DirectSensors.Open();
}
