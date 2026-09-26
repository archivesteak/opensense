using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

public static class LightingBackends
{
    /// <summary>A backend for each light detection found: the embedded controller's on its thread, HID and USB lights on the lighting thread.</summary>
    public static IReadOnlyList<ILightingBackend> Create(IDeviceDispatcher dispatcher, DeviceCapabilities caps, HidLights? hid = null,
        LightingWorker? worker = null)
    {
        List<ILightingBackend> backends = [];
        foreach (var light in caps.Lights)
        {
            switch (light.Backend)
            {
                case LightingBackendKind.EcKeyboard:
                    backends.Add(new EcKeyboardBackend(dispatcher, caps.Keyboard));
                    break;
                case LightingBackendKind.EcLightBar:
                    backends.Add(new EcLightBarBackend(dispatcher, caps.LightBars, caps.Smbios.LedArrayLength));
                    break;
                case LightingBackendKind.EcLogo:
                    backends.Add(new EcLogoBackend(dispatcher));
                    break;
                default:
                    if (hid is not null && worker is not null)
                        backends.AddRange(hid.CreateBackends(worker, [light]));
                    break;
            }
        }
        return backends;
    }
}
