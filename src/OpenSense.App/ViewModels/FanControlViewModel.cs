using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.App.ViewModels;

/// <summary>One fan in Custom mode: a fixed boost, or a boost that follows <see cref="Curve"/>.</summary>
public sealed partial class ManualFanViewModel(FanId id, string name, CurveFanViewModel curve) : ObservableObject
{
    public FanId Id { get; } = id;

    public string Name { get; } = name;

    public CurveFanViewModel Curve { get; } = curve;

    /// <summary>0 = fixed boost, 1 = curve (the order of the segmented control).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseCurve), nameof(IsFixed))]
    public partial int KindIndex { get; set; }

    public bool UseCurve => KindIndex == 1;

    public bool IsFixed => !UseCurve;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PercentText))]
    public partial double Percent { get; set; } = 30;

    public string PercentText => Units.Percent(Percent);

    public ManualFanSetting ToSetting() => new((int)Math.Round(Percent), UseCurve);
}

/// <summary>A fan's curve; it follows the fan's own chip (CPU fan: CPU, GPU fans: GPU).</summary>
public sealed partial class CurveFanViewModel(FanId id, FanChip chip, string name) : ObservableObject
{
    public FanId Id { get; } = id;

    public FanChip Chip { get; } = chip;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial FanCurve Curve { get; set; } = AntiThrottle.For(chip, ThermalLimits.Default);

    /// <summary>Where the chips start slowing down, as the engine last said: the Default preset goes by them.</summary>
    public ThermalLimits Limits { get; set; } = ThermalLimits.Default;

    /// <summary>Current source temperature (°C) for the editor's live marker; NaN when unknown.</summary>
    [ObservableProperty]
    public partial double LiveTemperature { get; set; } = double.NaN;

    /// <summary>Boost the fan is getting (%); NaN when unknown.</summary>
    [ObservableProperty]
    public partial double LivePercent { get; set; } = double.NaN;

    [ObservableProperty]
    public partial bool UseFahrenheit { get; set; }

    /// <summary>Auto's curve for this fan's chip.</summary>
    [RelayCommand]
    private void ApplyDefault() => Curve = AntiThrottle.For(Chip, Limits);

    public CurveFanSetting ToSetting() => new(Curve);
}

public sealed record OperatingModeOption(OperatingMode Mode, string Name, string Description);

/// <summary>Fan mode, Auto's boost, Custom boosts and curves, CoolBoost and operating modes.</summary>
public sealed partial class FanControlViewModel : ObservableObject
{
    private static readonly TimeSpan PushDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>Every operating mode, in the order they are listed.</summary>
    internal static readonly OperatingModeOption[] AllModes =
    [
        Option(OperatingMode.Eco),
        Option(OperatingMode.Quiet),
        Option(OperatingMode.Balanced),
        Option(OperatingMode.Performance),
        Option(OperatingMode.Turbo),
    ];

    private static OperatingModeOption Option(OperatingMode mode) => new(mode, Names.OperatingMode(mode), Names.OperatingModeDescription(mode));

    private readonly DeviceSession _session;
    private readonly MonitorViewModel _monitor;
    private readonly DispatcherQueueTimer _pushTimer;
    private bool _loading;
    private PowerLimit _limit;

    /// <summary>The user picked an operating mode since the last push.</summary>
    private bool _modeChosen;

    /// <summary>The user picked a fan curve since the last push.</summary>
    private bool _fanTableChosen;

    public FanControlViewModel(DispatcherQueue dispatcher, DeviceSession session, MonitorViewModel monitor)
    {
        _session = session;
        _monitor = monitor;
        _pushTimer = dispatcher.CreateTimer();
        _pushTimer.Interval = PushDelay;
        _pushTimer.IsRepeating = false;
        _pushTimer.Tick += (_, _) => Push();
        monitor.PropertyChanged += OnMonitorChanged;
    }

    /// <summary>In <see cref="FanControlMode"/> order.</summary>
    public static IReadOnlyList<string> ModeNames { get; } =
        [Names.FanMode(FanControlMode.Auto), Names.FanMode(FanControlMode.Max), Names.FanMode(FanControlMode.Custom)];

    /// <summary>Custom mode, one per fan.</summary>
    public ObservableCollection<ManualFanViewModel> ManualFans { get; } = [];

    public ObservableCollection<OperatingModeOption> OperatingModes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mode), nameof(ModeDescription), nameof(IsAuto), nameof(IsCustom), nameof(ShowCoolBoost), nameof(ShowFanTable))]
    public partial int ModeIndex { get; set; }

    public FanControlMode Mode => (FanControlMode)ModeIndex;

    public bool IsAuto => Mode == FanControlMode.Auto;

    public bool IsCustom => Mode == FanControlMode.Custom;

    public string ModeDescription => Names.FanModeDescription(Mode);

    /// <summary>Auto boosts the fans near the chips' limits (<see cref="AntiThrottle"/>).</summary>
    [ObservableProperty]
    public partial bool AutoBoostEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool HasFans { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial string? LockReason { get; set; }

    public bool IsLocked => LockReason is not null;

    [ObservableProperty]
    public partial bool Failsafe { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCoolBoost))]
    public partial bool CoolBoostAvailable { get; set; }

    /// <summary>CoolBoost raises the firmware's own speed, which Auto uses and Custom adds to; Max is full speed anyway.</summary>
    public bool ShowCoolBoost => CoolBoostAvailable && Mode != FanControlMode.Max;

    [ObservableProperty]
    public partial bool CoolBoost { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFanTable))]
    public partial bool FanTableAvailable { get; set; }

    /// <summary>The fan curve is the firmware's own speed, like CoolBoost's: Auto follows it and Custom adds to it.</summary>
    public bool ShowFanTable => FanTableAvailable && Mode != FanControlMode.Max;

    /// <summary>The embedded controller's fan curve: 0 Standard, 1 Faster, 2 Fastest.</summary>
    [ObservableProperty]
    public partial int FanTableIndex { get; set; }

    [ObservableProperty]
    public partial bool OperatingModesAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OperatingModeDescription))]
    public partial int OperatingModeIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string? OperatingModeNote { get; set; }

    public string OperatingModeDescription =>
        OperatingModeIndex >= 0 && OperatingModeIndex < OperatingModes.Count ? OperatingModes[OperatingModeIndex].Description : "";

    [ObservableProperty]
    public partial bool RestoreAutoOnExit { get; set; } = true;

    /// <summary>The laptop can run its fans backwards to blow dust out (Dust Defender).</summary>
    [ObservableProperty]
    public partial bool DustDefenderAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DustDefenderStatus))]
    [NotifyCanExecuteChangedFor(nameof(CleanFansCommand))]
    public partial bool DustDefenderRunning { get; set; }

    /// <summary>Why the last start didn't happen; cleared when a run starts.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DustDefenderStatus))]
    public partial string? DustDefenderRefusal { get; set; }

    public string DustDefenderStatus => DustDefenderRunning ? Strings.Get("DustDefender_Running")
        : DustDefenderRefusal ?? Strings.Get("DustDefender_Description");

    /// <summary>The laptop has a Mode key (seen once) and Turbo, so the key can be set to Turbo on and off.</summary>
    [ObservableProperty]
    public partial bool ModeKeyAvailable { get; set; }

    /// <summary>In <see cref="ModeKeyAction"/> order.</summary>
    [ObservableProperty]
    public partial int ModeKeyIndex { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        _pushTimer.Stop();
        var caps = _session.Capabilities;
        var profile = _session.Settings.Profile;

        HasFans = caps.ControllableFans.Count > 0;
        ModeIndex = (int)profile.Mode;
        AutoBoostEnabled = profile.AutoBoost;

        ManualFans.Clear();
        foreach (var fan in caps.ControllableFans)
        {
            var manual = profile.ManualFor(fan.Id);
            var name = Names.Fan(fan.Id, caps.Fans);
            var custom = new ManualFanViewModel(fan.Id, name, CreateCurve(fan, name, profile.CurveFor(fan.Id)))
            {
                KindIndex = manual.UseCurve ? 1 : 0,
                Percent = manual.Percent,
            };
            custom.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ManualFanViewModel.KindIndex) or nameof(ManualFanViewModel.Percent))
                    SchedulePush();
            };
            ManualFans.Add(custom);
        }

        CoolBoostAvailable = caps.CoolBoost;
        CoolBoost = profile.CoolBoost ?? _session.Firmware?.CoolBoost ?? false;

        // A controller where nothing has picked a curve yet runs the standard one.
        FanTableAvailable = caps.FanTable;
        FanTableIndex = (int)(profile.FanTable ?? _session.Firmware?.FanTable ?? FanTable.Standard) - 1;

        _limit = _session.Latest?.PowerLimit ?? PowerLimit.None;
        ListOperatingModes();

        RestoreAutoOnExit = profile.Safety.RestoreAutoOnExit;
        DustDefenderAvailable = caps.DustDefender;
        DustDefenderRunning = _session.Latest?.DustDefenderRunning == true;
        DustDefenderRefusal = null;
        ModeKeyAvailable = caps.ModeKey && caps.OperatingModes.Contains(OperatingMode.Turbo);
        ModeKeyIndex = (int)profile.ModeKey;
        _loading = false;
    }

    /// <summary>
    /// The modes the power supply allows now, with the one chosen for it selected: on battery the battery mode, else
    /// the AC mode (or, where the supply rules that out, the mode that runs instead).
    /// </summary>
    private void ListOperatingModes()
    {
        var loading = _loading;
        _loading = true;
        var caps = _session.Capabilities;
        var profile = _session.Settings.Profile;
        var allowed = OperatingModePolicy.Allowed(caps.OperatingModes, _limit, caps.ModeRules);
        OperatingModes.Clear();
        foreach (var option in AllModes.Where(m => allowed.Contains(m.Mode)))
            OperatingModes.Add(option);
        OperatingModesAvailable = OperatingModes.Count > 0;
        var shown = OperatingModePolicy.Target(profile, caps.OperatingModes, _limit, caps.ModeRules) ?? _session.Firmware?.OperatingMode;
        OperatingModeIndex = shown is { } mode ? IndexOf(mode) : -1;
        OperatingModeNote = _limit switch
        {
            PowerLimit.Battery => Strings.Get("OperatingMode_BatteryModeNote"),
            PowerLimit.LowBattery => Strings.Get("OperatingMode_LowBatteryNote"),
            PowerLimit.Adapter => Strings.Get("OperatingMode_AdapterNote"),
            _ => null,
        };
        _loading = loading;
        OnPropertyChanged(nameof(OperatingModes)); // the tray menu lists them too
    }

    private CurveFanViewModel CreateCurve(FanChannel fan, string name, CurveFanSetting setting)
    {
        var curve = new CurveFanViewModel(fan.Id, fan.Chip, name)
        {
            Curve = setting.Curve,
            Limits = _monitor.Latest?.Limits ?? ThermalLimits.Default,
            UseFahrenheit = _monitor.UseFahrenheit,
        };
        curve.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurveFanViewModel.Curve))
                SchedulePush();
        };
        return curve;
    }

    private IEnumerable<CurveFanViewModel> AllCurves => ManualFans.Select(m => m.Curve);

    private int IndexOf(OperatingMode mode)
    {
        for (var i = 0; i < OperatingModes.Count; i++)
        {
            if (OperatingModes[i].Mode == mode)
                return i;
        }
        return -1;
    }

    partial void OnModeIndexChanged(int value)
    {
        if (value >= 0) // selection controls briefly report -1 while initialising
            SchedulePush();
    }

    partial void OnAutoBoostEnabledChanged(bool value) => SchedulePush();

    partial void OnCoolBoostChanged(bool value) => SchedulePush();

    partial void OnFanTableIndexChanged(int value)
    {
        if (_loading || value < 0)
            return;
        _fanTableChosen = true;
        SchedulePush();
    }

    partial void OnOperatingModeIndexChanged(int value)
    {
        if (_loading || value < 0)
            return;
        _modeChosen = true;
        SchedulePush();
    }

    partial void OnModeKeyIndexChanged(int value) => SchedulePush();

    partial void OnRestoreAutoOnExitChanged(bool value) => SchedulePush();

    [RelayCommand]
    private void SetMode(string mode)
    {
        if (Enum.TryParse<FanControlMode>(mode, out var parsed))
            ModeIndex = (int)parsed;
    }

    /// <summary>Has the firmware blow the dust out now; the telemetry shows the run.</summary>
    [RelayCommand(CanExecute = nameof(CanCleanFans))]
    private async Task CleanFansAsync()
    {
        DustDefenderRefusal = null;
        var result = await _session.StartDustDefenderAsync();
        DustDefenderRefusal = result switch
        {
            DustDefenderStart.Busy => Strings.Get("DustDefender_Busy"),
            DustDefenderStart.Failed => Strings.Get("DustDefender_Failed"),
            _ => null,
        };
        if (result is DustDefenderStart.Started or DustDefenderStart.Running)
            DustDefenderRunning = true;
    }

    private bool CanCleanFans() => !DustDefenderRunning;

    private void SchedulePush()
    {
        if (_loading)
            return;
        _pushTimer.Stop();
        _pushTimer.Start();
    }

    private void Push()
    {
        var current = _session.Settings.Profile;
        var profile = current with
        {
            Mode = ModeIndex >= 0 ? Mode : current.Mode,
            AutoBoost = AutoBoostEnabled,
            Manual = ManualFans.ToDictionary(f => f.Id, f => f.ToSetting()),
            Curves = ManualFans.ToDictionary(f => f.Id, f => f.Curve.ToSetting()),
            CoolBoost = CoolBoostAvailable ? CoolBoost : current.CoolBoost,
            // Only a curve the user picked is written: until then the firmware keeps its own.
            FanTable = _fanTableChosen && FanTableAvailable && FanTableIndex >= 0 ? (FanTable)(FanTableIndex + 1) : current.FanTable,
            ModeKey = ModeKeyAvailable ? (ModeKeyAction)ModeKeyIndex : current.ModeKey,
            Safety = current.Safety with
            {
                RestoreAutoOnExit = RestoreAutoOnExit,
            },
        };
        // Only a mode the user picked is stored: the list shows what runs, which the supply may have chosen instead.
        if (_modeChosen && OperatingModeIndex >= 0 && OperatingModeIndex < OperatingModes.Count)
        {
            var chosen = OperatingModes[OperatingModeIndex].Mode;
            profile = _limit == PowerLimit.Battery ? profile with { BatteryOperatingMode = chosen } : profile with { OperatingMode = chosen };
        }
        _modeChosen = false;
        _fanTableChosen = false;
        _ = _session.SetProfileAsync(profile);
    }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorViewModel.UseFahrenheit))
        {
            foreach (var curve in AllCurves)
                curve.UseFahrenheit = _monitor.UseFahrenheit;
        }
        if (e.PropertyName != nameof(MonitorViewModel.Latest) || _monitor.Latest is not { } t)
            return;

        LockReason = t.FanLock is { } fanLock ? Names.FanLock(fanLock) : null;
        Failsafe = t.Failsafe;
        DustDefenderRunning = t.DustDefenderRunning == true;
        if (t.PowerLimit != _limit)
        {
            _limit = t.PowerLimit;
            ListOperatingModes();
        }

        foreach (var curve in AllCurves)
        {
            // As the engine does: a sleeping GPU has no temperature; one without a sensor follows the CPU.
            var temperature = curve.Chip == FanChip.Gpu
                ? t.GpuTemperature ?? (t.GpuAsleep ? null : t.CpuTemperature)
                : t.CpuTemperature;
            curve.LiveTemperature = temperature ?? double.NaN;
            curve.Limits = t.Limits;
            curve.LivePercent = t.Fan(curve.Id) is { } fan
                ? fan.Behavior == FanBehavior.Max ? 100 : fan.BoostPercent ?? 0
                : double.NaN;
        }
    }
}
