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

/// <summary>A fan's curve; it follows the fan's own chip (CPU fan: CPU, GPU fan: GPU).</summary>
public sealed partial class CurveFanViewModel(FanId id, string name) : ObservableObject
{
    public FanId Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial FanCurve Curve { get; set; } = CurvePresets.Default;

    /// <summary>Current source temperature (°C) for the editor's live marker; NaN when unknown.</summary>
    [ObservableProperty]
    public partial double LiveTemperature { get; set; } = double.NaN;

    /// <summary>Boost the fan is getting (%); NaN when unknown.</summary>
    [ObservableProperty]
    public partial double LivePercent { get; set; } = double.NaN;

    [ObservableProperty]
    public partial bool UseFahrenheit { get; set; }

    [RelayCommand]
    private void ApplyDefault() => Curve = CurvePresets.Default;

    public CurveFanSetting ToSetting() => new(Curve);
}

public sealed record OperatingModeOption(OperatingMode Mode, string Name, string Description, bool NeedsAc);

/// <summary>Fan mode, Auto's boost, Custom boosts and curves, CoolBoost and operating modes.</summary>
public sealed partial class FanControlViewModel : ObservableObject
{
    private static readonly TimeSpan PushDelay = TimeSpan.FromMilliseconds(120);

    private static readonly OperatingModeOption[] AllModes =
    [
        Option(OperatingMode.Eco, needsAc: false),
        Option(OperatingMode.Quiet, needsAc: false),
        Option(OperatingMode.Balanced, needsAc: false),
        Option(OperatingMode.Performance, needsAc: true),
        Option(OperatingMode.Turbo, needsAc: true),
    ];

    private static OperatingModeOption Option(OperatingMode mode, bool needsAc) =>
        new(mode, Names.OperatingMode(mode), Names.OperatingModeDescription(mode), needsAc);

    private readonly DeviceSession _session;
    private readonly MonitorViewModel _monitor;
    private readonly DispatcherQueueTimer _pushTimer;
    private bool _loading;

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
    [NotifyPropertyChangedFor(nameof(Mode), nameof(ModeDescription), nameof(IsAuto), nameof(IsCustom), nameof(ShowCoolBoost))]
    public partial int ModeIndex { get; set; }

    public FanControlMode Mode => (FanControlMode)ModeIndex;

    public bool IsAuto => Mode == FanControlMode.Auto;

    public bool IsCustom => Mode == FanControlMode.Custom;

    public string ModeDescription => Names.FanModeDescription(Mode);

    /// <summary>Auto boosts the fans when it gets hot (<see cref="ControlProfile.AutoBoostCurve"/>).</summary>
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

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        _pushTimer.Stop();
        var caps = _session.Capabilities;
        var profile = _session.Settings.Profile;

        HasFans = caps.Fans.Count > 0;
        ModeIndex = (int)profile.Mode;
        AutoBoostEnabled = profile.AutoBoost;

        ManualFans.Clear();
        foreach (var fan in caps.Fans)
        {
            var manual = profile.ManualFor(fan.Id);
            var custom = new ManualFanViewModel(fan.Id, Names.Fan(fan.Id), CreateCurve(fan.Id, profile.CurveFor(fan.Id)))
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

        OperatingModes.Clear();
        foreach (var option in AllModes.Where(m => caps.OperatingModes.Contains(m.Mode)))
            OperatingModes.Add(option);
        OperatingModesAvailable = OperatingModes.Count > 0;
        var currentMode = profile.OperatingMode ?? _session.Firmware?.OperatingMode;
        OperatingModeIndex = currentMode is { } mode ? IndexOf(mode) : -1;

        RestoreAutoOnExit = profile.Safety.RestoreAutoOnExit;
        _loading = false;
    }

    private CurveFanViewModel CreateCurve(FanId id, CurveFanSetting setting)
    {
        var curve = new CurveFanViewModel(id, Names.Fan(id))
        {
            Curve = setting.Curve,
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

    partial void OnOperatingModeIndexChanged(int value) => SchedulePush();

    partial void OnRestoreAutoOnExitChanged(bool value) => SchedulePush();

    [RelayCommand]
    private void SetMode(string mode)
    {
        if (Enum.TryParse<FanControlMode>(mode, out var parsed))
            ModeIndex = (int)parsed;
    }

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
            OperatingMode = OperatingModeIndex >= 0 && OperatingModeIndex < OperatingModes.Count
                ? OperatingModes[OperatingModeIndex].Mode
                : current.OperatingMode,
            Safety = current.Safety with
            {
                RestoreAutoOnExit = RestoreAutoOnExit,
            },
        };
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
        OperatingModeNote = !t.OnAcPower && OperatingModes.Any(m => m.NeedsAc)
            ? Strings.Format("OperatingMode_BatteryNote", Names.OperatingMode(OperatingMode.Performance), Names.OperatingMode(OperatingMode.Turbo),
                Names.OperatingMode(OperatingMode.Balanced))
            : null;

        foreach (var curve in AllCurves)
        {
            // As the engine does: a sleeping GPU has no temperature; one without a sensor follows the CPU.
            var temperature = curve.Id == FanId.Gpu
                ? t.GpuTemperature ?? (t.GpuAsleep ? null : t.CpuTemperature)
                : t.CpuTemperature;
            curve.LiveTemperature = temperature ?? double.NaN;
            curve.LivePercent = t.Fan(curve.Id) is { } fan
                ? fan.Behavior == FanBehavior.Max ? 100 : fan.BoostPercent ?? 0
                : double.NaN;
        }
    }
}
