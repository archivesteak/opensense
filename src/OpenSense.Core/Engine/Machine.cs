using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Engine;

/// <summary>Everything the engine reads the laptop through, so tests can put a simulated laptop behind it.</summary>
public interface IMachine
{
    /// <summary>E.g. "Nitro AN515-57"; null when Windows does not say.</summary>
    string? Model { get; }

    string? BiosVersion { get; }

    /// <summary>The serial number on the laptop's label; null when Windows does not say.</summary>
    string? SerialNumber { get; }

    /// <summary>Acer's gaming WMI interface.</summary>
    IWmiTransport OpenFirmware();

    /// <summary>The firmware's events (Mode key, adapter, battery).</summary>
    IFirmwareEvents OpenFirmwareEvents();

    /// <summary>AC adapter and battery, as Windows reports them.</summary>
    IPowerSource OpenPowerSource();

    /// <summary>Windows' power management (keeping it awake, power plan settings).</summary>
    ISystemPower OpenSystemPower();

    /// <summary>The battery's wear, as it reports it to Windows; null without a battery.</summary>
    BatteryHealth? ReadBatteryHealth();

    /// <summary>HID devices: Acer's embedded-controller interface on newer models, USB lighting devices.</summary>
    IHidBus OpenHid();

    /// <summary>What NitroSense's installer left behind, used as detection hints.</summary>
    NitroSenseHints ReadHints();

    AcerSmbios ReadSmbios();

    ILoadMonitor OpenLoadMonitor();

    /// <summary>The CPU's and GPU's own temperature sensors, where available.</summary>
    DirectSensors OpenSensors();

    /// <summary>Where the firmware finds a custom boot logo; null without an EFI system partition.</summary>
    IBootLogoStore? OpenBootLogo(Action<string> log);

    /// <summary>The built-in screen's native resolution, when Windows lists it.</summary>
    PixelSize? ReadInternalScreen();
}

/// <summary>This PC.</summary>
public sealed class WindowsMachine : IMachine
{
    public string? Model => SystemInfo.ReadModel();

    public string? BiosVersion => SystemInfo.ReadBiosVersion();

    public string? SerialNumber => SystemInfo.ReadSerialNumber();

    public IWmiTransport OpenFirmware() => new WmiTransport();

    public IFirmwareEvents OpenFirmwareEvents() => new WmiFirmwareEvents();

    public IPowerSource OpenPowerSource() => new SystemPowerSource();

    public ISystemPower OpenSystemPower() => new WindowsSystemPower();

    public BatteryHealth? ReadBatteryHealth() => WindowsBattery.Read();

    public IHidBus OpenHid() => new WindowsHidBus();

    public NitroSenseHints ReadHints() => NitroSenseHints.Read();

    public AcerSmbios ReadSmbios() => AcerSmbios.Read();

    public ILoadMonitor OpenLoadMonitor() => new LoadMonitor();

    public DirectSensors OpenSensors() => DirectSensors.Open();

    public IBootLogoStore? OpenBootLogo(Action<string> log) => BootLogoFolder.OpenSystemPartition(log);

    public PixelSize? ReadInternalScreen() => InternalScreen.ReadNativeResolution();
}
