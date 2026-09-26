using System.Management;

namespace OpenSense.Core.Hardware.Boot;

/// <summary>The laptop's own screen, where the firmware shows the boot logo.</summary>
public static class InternalScreen
{
    // D3DKMDT_VIDEO_OUTPUT_TECHNOLOGY values of built-in panels: LVDS, embedded DisplayPort, embedded UDI, internal.
    private static readonly HashSet<uint> BuiltIn = [6, 11, 13, 0x80000000];

    /// <summary>
    /// The native resolution (the monitor's preferred mode) of the built-in panel, or null when Windows doesn't list
    /// it, e.g. with the lid closed. Works in a service: the monitor driver's WMI classes need no desktop.
    /// </summary>
    public static PixelSize? ReadNativeResolution()
    {
        try
        {
            var builtIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connections = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, VideoOutputTechnology FROM WmiMonitorConnectionParams"))
            {
                foreach (var connection in connections.Get())
                {
                    using (connection)
                    {
                        if (connection["VideoOutputTechnology"] is uint technology && BuiltIn.Contains(technology) && connection["InstanceName"] is string name)
                            builtIn.Add(name);
                    }
                }
            }

            using var modes = new ManagementObjectSearcher(@"root\wmi",
                "SELECT InstanceName, MonitorSourceModes, PreferredMonitorSourceModeIndex FROM WmiMonitorListedSupportedSourceModes");
            foreach (var monitor in modes.Get())
            {
                using (monitor)
                {
                    if (monitor["InstanceName"] is not string name || !builtIn.Contains(name)
                        || monitor["MonitorSourceModes"] is not ManagementBaseObject[] list
                        || monitor["PreferredMonitorSourceModeIndex"] is not ushort preferred || preferred >= list.Length)
                        continue;
                    var mode = list[preferred];
                    if (mode["HorizontalActivePixels"] is ushort width && mode["VerticalActivePixels"] is ushort height && width > 0 && height > 0)
                        return new PixelSize(width, height);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
        }
        return null;
    }
}
