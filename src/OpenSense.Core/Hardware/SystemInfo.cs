using System.Management;
using System.Security.Principal;

namespace OpenSense.Core.Hardware;

public interface IPowerSource
{
    bool IsOnAcPower { get; }
}

public sealed class SystemPowerSource : IPowerSource
{
    public bool IsOnAcPower => !Windows.Win32.PInvoke.GetSystemPowerStatus(out var status) || status.ACLineStatus != 0;
}

public static class SystemInfo
{
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>Manufacturer and model, e.g. "Acer Nitro AN515-57". Does not need elevation.</summary>
    public static string? ReadModel()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            foreach (var item in searcher.Get())
                return $"{item["Manufacturer"]} {item["Model"]}".Trim();
        }
        catch (ManagementException)
        {
        }
        return null;
    }

    public static string? ReadBiosVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
            foreach (var item in searcher.Get())
                return item["SMBIOSBIOSVersion"]?.ToString();
        }
        catch (ManagementException)
        {
        }
        return null;
    }
}
