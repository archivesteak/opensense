using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.App.ViewModels;

/// <summary>One operating mode's GPU clock offsets, or the only set on laptops without operating modes.</summary>
public sealed partial class GpuClockRowViewModel(OperatingMode? mode, string name, Action<GpuClockRowViewModel> changed) : ObservableObject
{
    /// <summary>Wide enough to keep any saved value until the driver's limits are known.</summary>
    private const double Unbounded = 100_000;

    public OperatingMode? Mode { get; } = mode;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial double Core { get; set; }

    [ObservableProperty]
    public partial double Memory { get; set; }

    [ObservableProperty]
    public partial double CoreMinimum { get; set; } = -Unbounded;

    [ObservableProperty]
    public partial double CoreMaximum { get; set; } = Unbounded;

    [ObservableProperty]
    public partial double MemoryMinimum { get; set; } = -Unbounded;

    [ObservableProperty]
    public partial double MemoryMaximum { get; set; } = Unbounded;

    /// <summary>The driver's limits are known.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editable))]
    public partial bool LimitsKnown { get; set; }

    /// <summary>Overclocking is switched on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editable))]
    public partial bool Unlocked { get; set; }

    /// <summary>The offsets can be edited: overclocking is on and the driver's limits are known.</summary>
    public bool Editable => LimitsKnown && Unlocked;

    /// <summary>The firmware's overclock for the mode, where it is followed, and "In use now" on the mode in force.</summary>
    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>"Acer: +100 MHz core, +200 MHz memory" where the firmware's overclock for the mode is followed.</summary>
    public string? Firmware { get; set; }

    public ClockOffsets Offsets => new((int)Core, (int)Memory);

    public void ShowStatus(bool inUse) =>
        Status = string.Join(Environment.NewLine, new[] { Firmware, inUse ? Strings.Get("GpuClocks_InUse") : null }.OfType<string>()) is { Length: > 0 } text
            ? text
            : null;

    public void SetLimits(GpuClockLimits limits)
    {
        CoreMinimum = limits.CoreMin;
        CoreMaximum = limits.CoreMax;
        MemoryMinimum = limits.MemoryMin;
        MemoryMaximum = limits.MemoryMax;
        LimitsKnown = true;
    }

    // Whole MHz only, and an emptied box means no offset.
    partial void OnCoreChanged(double value)
    {
        if (Whole(value) is { } whole)
            Core = whole;
        else
            changed(this);
    }

    partial void OnMemoryChanged(double value)
    {
        if (Whole(value) is { } whole)
            Memory = whole;
        else
            changed(this);
    }

    /// <summary>The value to use instead, or null when <paramref name="value"/> is fine as it is.</summary>
    private static double? Whole(double value) => double.IsNaN(value) ? 0 : value != Math.Round(value) ? Math.Round(value) : null;
}

/// <summary>
/// The discrete GPU's clock offsets (NVIDIA), per operating mode like Acer's software: shown once its driver has
/// said it takes them, and editable within the limits it reported. Where the firmware has its own overclock for the
/// modes (2024+ Predators), that is added to them unless turned off.
/// </summary>
public sealed partial class GpuClocksViewModel : ObservableObject
{
    private readonly DeviceSession _session;
    private bool _loading;
    private bool _perMode;
    private IReadOnlyDictionary<OperatingMode, ClockOffsets>? _firmware;

    public GpuClocksViewModel(DispatcherQueue dispatcher, DeviceSession session)
    {
        _session = session;
        session.TelemetryUpdated += telemetry => dispatcher.TryEnqueue(() => Show(telemetry));
    }

    public ObservableCollection<GpuClockRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial bool Available { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    /// <summary>Some offset is set (so there is something to reset).</summary>
    [ObservableProperty]
    public partial bool Overclocked { get; set; }

    /// <summary>The firmware has its own overclock for some operating modes.</summary>
    [ObservableProperty]
    public partial bool FirmwareOverclock { get; set; }

    /// <summary>The firmware's overclock is added to the offsets set here.</summary>
    [ObservableProperty]
    public partial bool FollowFirmware { get; set; }

    /// <summary>Overclocking is switched on: the offsets set here apply, and can be edited.</summary>
    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>The engine switched overclocking off because Windows stopped while it was on.</summary>
    [ObservableProperty]
    public partial bool TurnedOffAfterCrash { get; set; }

    /// <summary>
    /// Asked when overclocking is switched on; true to go ahead. Only the switch asks: a dialog opened by an offset's
    /// spin button leaves the button pressed, and it then steps on its own whenever the pointer is over it (issue #17).
    /// </summary>
    public Func<Task<bool>>? ConfirmOverclock { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        var caps = _session.Capabilities;
        var settings = _session.Settings.Profile.GpuClocks;
        _perMode = caps.HasOperatingModes;
        _firmware = _perMode ? caps.EcHid?.GpuOffsets : null;
        FirmwareOverclock = _firmware is { Count: > 0 };
        FollowFirmware = settings.FollowFirmware;
        Enabled = settings.Enabled;
        TurnedOffAfterCrash = settings.TurnedOffAfterCrash;
        Rows.Clear();
        if (_perMode)
        {
            foreach (var option in FanControlViewModel.AllModes.Where(m => caps.OperatingModes.Contains(m.Mode)))
                Rows.Add(Row(option.Mode, option.Name, settings.Modes.GetValueOrDefault(option.Mode) ?? ClockOffsets.None));
        }
        else
        {
            Rows.Add(Row(null, Strings.Get("GpuClocks_Always"), settings.Fixed));
        }
        Overclocked = Rows.Any(r => !r.Offsets.IsNone);
        ShowFirmware();
        Show(_session.Latest);
        _loading = false;
    }

    partial void OnFollowFirmwareChanged(bool value)
    {
        if (_loading)
            return;
        ShowFirmware();
        Show(_session.Latest);
        Push();
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_loading)
            return;
        if (value)
        {
            _ = SwitchOnAsync();
            return;
        }
        Unlock(false);
        Push();
    }

    /// <summary>Only after the warning do the offsets unlock and reach the engine; otherwise the switch goes back off.</summary>
    private async Task SwitchOnAsync()
    {
        if (ConfirmOverclock is not { } confirm || await confirm())
        {
            Unlock(true);
            Push();
            return;
        }
        _loading = true;
        Enabled = false;
        _loading = false;
    }

    private void Unlock(bool unlocked)
    {
        foreach (var row in Rows)
            row.Unlocked = unlocked;
    }

    /// <summary>Each mode's own overclock from the firmware, while it is followed.</summary>
    private void ShowFirmware()
    {
        foreach (var row in Rows)
        {
            row.Firmware = FollowFirmware && row.Mode is { } mode && _firmware?.GetValueOrDefault(mode) is { } acer
                ? Strings.Format("GpuClocks_Firmware", Signed(acer.CoreMhz), Signed(acer.MemoryMhz))
                : null;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _loading = true;
        foreach (var row in Rows)
        {
            row.Core = 0;
            row.Memory = 0;
        }
        _loading = false;
        Overclocked = false;
        Push();
    }

    private GpuClockRowViewModel Row(OperatingMode? mode, string name, ClockOffsets offsets) =>
        new(mode, name, OnRowChanged) { Core = offsets.CoreMhz, Memory = offsets.MemoryMhz, Unlocked = Enabled };

    private void OnRowChanged(GpuClockRowViewModel row)
    {
        if (_loading)
            return;
        Overclocked = Rows.Any(r => !r.Offsets.IsNone);
        Push();
    }

    private void Push()
    {
        var profile = _session.Settings.Profile;
        var clocks = profile.GpuClocks;
        if (_perMode)
        {
            // Modes this laptop doesn't list keep whatever they had.
            var modes = new Dictionary<OperatingMode, ClockOffsets>(clocks.Modes);
            foreach (var row in Rows)
            {
                if (row.Mode is not { } mode)
                    continue;
                if (row.Offsets.IsNone)
                    modes.Remove(mode);
                else
                    modes[mode] = row.Offsets;
            }
            clocks = clocks with { Modes = modes };
        }
        else
        {
            clocks = clocks with { Fixed = Rows[0].Offsets };
        }
        // Switching either back on answers the warning about the crash.
        TurnedOffAfterCrash &= !Enabled && !FollowFirmware;
        _ = _session.SetProfileAsync(profile with
        {
            GpuClocks = clocks with { Enabled = Enabled, FollowFirmware = FollowFirmware, TurnedOffAfterCrash = TurnedOffAfterCrash },
        });
    }

    private void Show(Telemetry? telemetry)
    {
        var clocks = telemetry?.GpuClocks;
        Available = clocks is not null;
        if (clocks is null)
            return;

        foreach (var row in Rows)
        {
            if (clocks.Limits is { } limits)
                row.SetLimits(limits);
            row.ShowStatus(_perMode && row.Mode == telemetry!.OperatingMode);
        }
        Status = clocks switch
        {
            { Limits: null } => Strings.Get("GpuClocks_WaitingForLimits"),
            { Target: null } => Strings.Get("GpuClocks_Description"),
            { Applied: null } => Strings.Get("GpuClocks_Pending"),
            { Applied: { } applied, Target: { } target } when applied == target =>
                Strings.Format("GpuClocks_Applied", Signed(target.CoreMhz), Signed(target.MemoryMhz)),
            _ => Strings.Get("GpuClocks_NotApplied"),
        };
    }

    /// <summary>+150, −100 or 0, in the user's number format.</summary>
    private static string Signed(int mhz) => mhz switch
    {
        > 0 => "+" + mhz.ToString(CultureInfo.CurrentCulture),
        < 0 => CultureInfo.CurrentCulture.NumberFormat.NegativeSign + (-mhz).ToString(CultureInfo.CurrentCulture),
        _ => mhz.ToString(CultureInfo.CurrentCulture),
    };
}
