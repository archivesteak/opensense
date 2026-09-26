using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Lighting;

/// <summary>
/// The lights on HID and USB devices, opened for one engine session: the embedded controller's lighting interface
/// (2024+), a per-key USB keyboard (with its MagForce keys on the models that have them) and Darfon lights. A light
/// appears only when its device answers. Disposed when the devices come or go, and found again.
/// </summary>
public sealed class HidLights : IDisposable
{
    private readonly List<(LightingDeviceInfo Light, Func<LightingWorker, LightingDeviceInfo, ILightingBackend> Create)> _lights = [];

    private HidLights() { }

    public static HidLights None { get; } = new();

    public Kyd100Device? Kyd100 { get; private set; }

    /// <summary>The lights the lighting interface lists and describes.</summary>
    public IReadOnlyList<Kyd100LightInfo> Kyd100Lights { get; private set; } = [];

    /// <summary>A USB keyboard Acer's software knows (lights and settings, or settings only).</summary>
    public UsbKeyboardDevice? Keyboard { get; private set; }

    /// <summary>The keyboard answered its settings (the Windows key, backlight auto-off), which then go through it.</summary>
    public bool KeyboardSettings { get; private set; }

    public IReadOnlyList<DarfonDevice> Darfon { get; private set; } = [];

    /// <summary>The MagForce keys' light, which a Sunrex keyboard can have; null without one.</summary>
    public LightingDeviceInfo? MagKeyLight { get; private set; }

    /// <summary>The model is one Acer's software gives MagForce keys.</summary>
    public bool MagKeyDetected { get; private set; }

    /// <summary>The lights found (the MagForce keys' only when detected), ids unique among them.</summary>
    public IReadOnlyList<LightingDeviceInfo> Lights => [.. _lights.Select(l => l.Light).Where(l => l.Backend != LightingBackendKind.MagKey || MagKeyDetected)];

    /// <summary>The HID interfaces that make up these lights, to tell whether an arrival or removal concerns them.</summary>
    public IReadOnlySet<string> Paths { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>An interface OpenSense would drive lights (or keyboard settings) through.</summary>
    public static bool Relevant(HidDeviceInfo device) => Kyd100Protocol.Matches(device) || UsbKeyboardProtocol.IsCommandInterface(device)
        || UsbKeyboardProtocol.IsLightingInterface(device) || DarfonProtocol.Matches(device);

    /// <param name="model">The laptop's model name (MagForce keys are told by it).</param>
    /// <param name="sleep">Waits between reports (tests pass a no-op).</param>
    public static HidLights Open(IHidBus bus, string? model, Action<string> log, Action<TimeSpan>? sleep = null)
    {
        var found = new HidLights();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Kyd100Device.Open(bus, sleep) is { } kyd)
        {
            found.Kyd100 = kyd;
            paths.Add(kyd.Info.Path);
            log($"Lighting HID: {kyd.Info}; values {string.Join(" ", kyd.Info.FeatureValues.Select(v => $"{v.ReportId:X2}/{v.Usage:X2}:{v.BitSize}x{v.ReportCount}"))}");
            var ids = kyd.ReadLightIds();
            log($"Lighting HID lights: {(ids is null ? "no answer" : string.Join(" ", ids.Select(i => $"{i:X2}")))}");
            List<Kyd100LightInfo> lights = [];
            foreach (var id in ids ?? [])
            {
                if (!Enum.IsDefined((Kyd100Light)id))
                    continue;
                var info = kyd.ReadInfo((Kyd100Light)id);
                log($"Lighting HID light {id:X2}: {(info is { } i ? $"{i.Leds} LEDs, attributes 0x{i.Attributes:X}" : "no answer")}");
                if (info is not null)
                    lights.Add(info);
            }
            found.Kyd100Lights = lights;
            foreach (var light in lights)
                found.Add(Kyd100Backend.Describe(light), (worker, info) => new Kyd100Backend(worker, kyd, info));
        }

        if (UsbKeyboardDevice.Open(bus, sleep) is { } keyboard)
        {
            found.Keyboard = keyboard;
            paths.Add(keyboard.Info.Path);
            var model2 = keyboard.Model;
            var windowsKey = keyboard.ReadWindowsKeyEnabled();
            var autoOff = keyboard.ReadAutoOff();
            found.KeyboardSettings = windowsKey is not null || autoOff is not null;
            log($"USB keyboard: {keyboard.Info}; {model2.Maker} {model2.Generation} {model2.Layout?.ToString() ?? "unnamed layout"}; " +
                $"lighting interface {(keyboard.HasLighting ? "yes" : "no")}; per-key table {(keyboard.Leds is { } leds ? $"{leds.Count} keys" : "none")}; " +
                $"Windows key {(windowsKey is { } w ? w ? "on" : "locked" : "no answer")}; auto-off {(autoOff is { } a ? a ? "on" : "off" : "no answer")}");
            if (model2.Lighting && keyboard.HasLighting)
            {
                found.Add(UsbKeyboardBackend.Describe(keyboard), (worker, info) => new UsbKeyboardBackend(worker, keyboard, info));
                if (model2.Maker == UsbKeyboardMaker.Sunrex)
                {
                    var year = UsbKeyboardProtocol.MagKeyModel2025(model);
                    found.MagKeyDetected = year is not null;
                    // Forced on for a model Acer's software doesn't name: the newest list.
                    found.MagKeyLight = MagKeyBackend.Describe(year ?? true);
                    found.Add(found.MagKeyLight, (worker, info) => new MagKeyBackend(worker, keyboard, info));
                    log($"MagForce keys: {(year is null ? "not this model" : year.Value ? "2025 model" : "2024 model")}");
                }
            }
        }

        var darfon = DarfonDevice.OpenAll(bus, sleep);
        found.Darfon = darfon;
        foreach (var device in darfon)
        {
            paths.Add(device.Info.Path);
            log($"Darfon light: {device.Info}; {string.Join(", ", device.Model.Parts)}");
            foreach (var part in device.Model.Parts)
                found.Add(DarfonBackend.Describe(device.Model, part), (worker, info) => new DarfonBackend(worker, device, part, info));
        }

        found.Paths = paths;
        return found;
    }

    /// <summary>
    /// The embedded controller's capabilities with these lights: the lighting interface's keyboard (or a per-key USB
    /// keyboard) replaces the embedded controller's keyboard colours, any other light of the lighting interface its
    /// light bars and logo, as in Acer's software. A USB keyboard that answers takes the Windows key, and backlight
    /// auto-off unless the embedded controller's HID interface keeps that.
    /// </summary>
    public DeviceCapabilities Merge(DeviceCapabilities ec)
    {
        var lights = ec.Lights.ToList();
        if (Kyd100Lights.Any(l => l.Id == Kyd100Light.Keyboard) || _lights.Any(l => l.Light.Backend == LightingBackendKind.UsbKeyboard))
            lights.RemoveAll(l => l.Backend == LightingBackendKind.EcKeyboard);
        if (Kyd100Lights.Any(l => l.Id != Kyd100Light.Keyboard))
            lights.RemoveAll(l => l.Backend is LightingBackendKind.EcLightBar or LightingBackendKind.EcLogo);
        foreach (var light in Lights)
            lights.Add(light with { Id = UniqueId(lights, light.Id) });

        var keyboard = ec.Keyboard;
        if (KeyboardSettings)
        {
            var autoOff = !keyboard.EcHidBacklightTimeout;
            keyboard = keyboard with
            {
                WindowsKey = true,
                UsbWindowsKey = true,
                UsbBacklightTimeout = autoOff,
                BacklightHotkey = autoOff ? null : keyboard.BacklightHotkey,
            };
        }
        return ec with
        {
            Lights = lights,
            Keyboard = keyboard,
            MagKeyLight = MagKeyLight is { } mag ? mag with { Id = UniqueId(lights.Where(l => l.Source != mag.Source).ToList(), mag.Id) } : null,
        };
    }

    /// <summary>A backend for each light in <paramref name="lights"/> that is one of these (found by its source).</summary>
    public IReadOnlyList<ILightingBackend> CreateBackends(LightingWorker worker, IEnumerable<LightingDeviceInfo> lights)
    {
        List<ILightingBackend> backends = [];
        foreach (var light in lights)
        {
            if (light.Source is { } source && _lights.FirstOrDefault(l => l.Light.Source == source) is { Create: { } create })
                backends.Add(create(worker, light));
        }
        return backends;
    }

    private void Add(LightingDeviceInfo light, Func<LightingWorker, LightingDeviceInfo, ILightingBackend> create)
    {
        var taken = _lights.Select(l => l.Light).ToList();
        _lights.Add((light with { Id = UniqueId(taken, light.Id) }, create));
    }

    /// <summary><paramref name="id"/>, or with a number after it when a light already has it (a second light bar: "LightBar2").</summary>
    private static string UniqueId(IReadOnlyCollection<LightingDeviceInfo> lights, string id)
    {
        var unique = id;
        for (var n = 2; lights.Any(l => l.Id == unique); n++)
            unique = id + n;
        return unique;
    }

    public void Dispose()
    {
        Kyd100?.Dispose();
        Keyboard?.Dispose();
        foreach (var device in Darfon)
            device.Dispose();
    }
}
