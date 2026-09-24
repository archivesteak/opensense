using System.Globalization;
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

    /// <summary>The serial number on the label under the laptop (SMBIOS system serial). Does not need elevation.</summary>
    public static string? ReadSerialNumber()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (var item in searcher.Get())
                return item["SerialNumber"]?.ToString()?.Trim() is { Length: > 0 } serial ? serial : null;
        }
        catch (ManagementException)
        {
        }
        return null;
    }

    /// <summary>
    /// Acer's SNID, the all-digit form of a 22-character Acer serial number, as Acer Care Center shows it: characters
    /// 11 to 13 as they are, characters 14 to 18 (hexadecimal) in decimal and padded to six digits, character 19 as it
    /// is, and character 20 as a number (a digit as itself, a letter counting from 10 for A). Null for other serials.
    /// </summary>
    public static string? Snid(string? serialNumber)
    {
        if (serialNumber is not { Length: 22 }
            || !int.TryParse(serialNumber.AsSpan(13, 5), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var middle))
            return null;
        var last = char.ToUpperInvariant(serialNumber[19]) switch
        {
            >= '0' and <= '9' and var digit => digit - '0',
            >= 'A' and <= 'Z' and var letter => letter - 'A' + 10,
            _ => -1,
        };
        return last < 0
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{serialNumber.AsSpan(10, 3)}{middle:D6}{serialNumber[18]}{last}");
    }
}
