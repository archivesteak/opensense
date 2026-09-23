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

public sealed partial class ManualFanViewModel(FanId id, string name) : ObservableObject
{
    public FanId Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManual))]
    public partial bool Auto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PercentText))]
    public partial double Percent { get; set; } = 50;

    public bool IsManual => !Auto;

    public string PercentText => Units.Percent(Percent);

    public ManualFanSetting ToSetting() => new(Auto, (int)Math.Round(Percent));
}

public sealed partial class CurveFanViewModel(FanId id, string name) : ObservableObject
{
    /// <summary>In <see cref="TemperatureSource"/> order.</summary>
    public static IReadOnlyList<string> Sources { get; } =
        [Strings.Get("CurveSource_Cpu"), Strings.Get("CurveSource_Gpu"), Strings.Get("CurveSource_Hottest")];

    public FanId Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial FanCurve Curve { get; set; } = CurvePresets.Balanced;

    [ObservableProperty]
    public partial int SourceIndex { get; set; }

    /// <summary>Current source temperature (°C) for the editor's live marker; NaN when unknown.</summary>
    [ObservableProperty]
    public partial double LiveTemperature { get; set; } = double.NaN;

    /// <summary>Duty the fan is running at (%); NaN when unknown.</summary>
    [ObservableProperty]
    public partial double LivePercent { get; set; } = double.NaN;

    [ObservableProperty]
    public partial bool UseFahrenheit { get; set; }

    /// <summary>Raised when the user asks to copy this curve to the other fans.</summary>
    public event Action<CurveFanViewModel>? CopyRequested;

    [RelayCommand]
    private void CopyToOthers() => CopyRequested?.Invoke(this);

    public TemperatureSource Source => (TemperatureSource)SourceIndex;

    [RelayCommand]
    private void ApplyPreset(string name) =>
        Curve = CurvePresets.All.FirstOrDefault(p => p.Name == name).Curve ?? Curve;

    public CurveFanSetting ToSetting() => new(Curve, Source);
}

public sealed record OperatingModeOption(OperatingMode Mode, string Name, string Description, bool NeedsAc);

/// <summary>Fan mode, manual speeds, curves, CoolBoost and operating modes.</summary>
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
        [Names.FanMode(FanControlMode.Auto), Names.FanMode(FanControlMode.Max), Names.FanMode(FanControlMode.Custom), Names.FanMode(FanControlMode.Curve)];

    public ObservableCollection<ManualFanViewModel> ManualFans { get; } = [];

    public ObservableCollection<CurveFanViewModel> CurveFans { get; } = [];

    public ObservableCollection<OperatingModeOption> OperatingModes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mode), nameof(ModeDescription), nameof(IsCustom), nameof(IsCurve))]
    public partial int ModeIndex { get; set; }

    public FanControlMode Mode => (FanControlMode)ModeIndex;

    public bool IsCustom => Mode == FanControlMode.Custom;

    public bool IsCurve => Mode == FanControlMode.Curve;

    public string ModeDescription => Names.FanModeDescription(Mode);

    [ObservableProperty]
    public partial bool HasFans { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial string? LockReason { get; set; }

    public bool IsLocked => LockReason is not null;

    [ObservableProperty]
    public partial bool Failsafe { get; set; }

    [ObservableProperty]
    public partial bool CoolBoostAvailable { get; set; }

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
    public partial double EmergencyTemperature { get; set; } = 95;

    [ObservableProperty]
    public partial double MinimumPercent { get; set; }

    [ObservableProperty]
    public partial double Hysteresis { get; set; } = 3;

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

        ManualFans.Clear();
        CurveFans.Clear();
        foreach (var fan in caps.Fans)
        {
            var manual = new ManualFanViewModel(fan.Id, Names.Fan(fan.Id)) { Auto = profile.ManualFor(fan.Id).Auto, Percent = profile.ManualFor(fan.Id).Percent };
            manual.PropertyChanged += (_, _) => SchedulePush();
            ManualFans.Add(manual);

            var curve = new CurveFanViewModel(fan.Id, Names.Fan(fan.Id)) { Curve = profile.CurveFor(fan.Id).Curve, SourceIndex = (int)profile.CurveFor(fan.Id).Source };
            curve.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurveFanViewModel.Curve) or nameof(CurveFanViewModel.SourceIndex))
                    SchedulePush();
            };
            curve.CopyRequested += CopyCurveToOtherFans;
            curve.UseFahrenheit = _monitor.UseFahrenheit;
            CurveFans.Add(curve);
        }

        CoolBoostAvailable = caps.CoolBoost;
        CoolBoost = profile.CoolBoost ?? _session.Firmware?.CoolBoost ?? false;

        OperatingModes.Clear();
        foreach (var option in AllModes.Where(m => caps.OperatingModes.Contains(m.Mode)))
            OperatingModes.Add(option);
        OperatingModesAvailable = OperatingModes.Count > 0;
        var currentMode = profile.OperatingMode ?? _session.Firmware?.OperatingMode;
        OperatingModeIndex = currentMode is { } mode ? IndexOf(mode) : -1;

        EmergencyTemperature = profile.Safety.EmergencyTemperatureC;
        MinimumPercent = profile.Safety.MinimumPercent;
        RestoreAutoOnExit = profile.Safety.RestoreAutoOnExit;
        Hysteresis = profile.Response.HysteresisC;
        _loading = false;
    }

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

    partial void OnCoolBoostChanged(bool value) => SchedulePush();

    partial void OnOperatingModeIndexChanged(int value) => SchedulePush();

    partial void OnEmergencyTemperatureChanged(double value) => SchedulePush();

    partial void OnMinimumPercentChanged(double value) => SchedulePush();

    partial void OnHysteresisChanged(double value) => SchedulePush();

    partial void OnRestoreAutoOnExitChanged(bool value) => SchedulePush();

    [RelayCommand]
    private void SetMode(string mode)
    {
        if (Enum.TryParse<FanControlMode>(mode, out var parsed))
            ModeIndex = (int)parsed;
    }

    private void CopyCurveToOtherFans(CurveFanViewModel source)
    {
        foreach (var other in CurveFans.Where(c => c != source))
            other.Curve = source.Curve;
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
            Manual = ManualFans.ToDictionary(f => f.Id, f => f.ToSetting()),
            Curves = CurveFans.ToDictionary(f => f.Id, f => f.ToSetting()),
            CoolBoost = CoolBoostAvailable ? CoolBoost : current.CoolBoost,
            OperatingMode = OperatingModeIndex >= 0 && OperatingModeIndex < OperatingModes.Count
                ? OperatingModes[OperatingModeIndex].Mode
                : current.OperatingMode,
            Response = current.Response with { HysteresisC = (int)Math.Round(Hysteresis) },
            Safety = current.Safety with
            {
                EmergencyTemperatureC = (int)Math.Round(EmergencyTemperature),
                MinimumPercent = (int)Math.Round(MinimumPercent),
                RestoreAutoOnExit = RestoreAutoOnExit,
            },
        };
        _ = _session.SetProfileAsync(profile);
    }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorViewModel.UseFahrenheit))
        {
            foreach (var curve in CurveFans)
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

        foreach (var curve in CurveFans)
        {
            var temperature = curve.Source switch
            {
                TemperatureSource.Cpu => t.CpuTemperature,
                TemperatureSource.Gpu => t.GpuTemperature ?? t.CpuTemperature,
                _ => t.Hottest,
            };
            curve.LiveTemperature = temperature ?? double.NaN;
            var fan = t.Fan(curve.Id);
            curve.LivePercent = fan?.CommandedPercent ?? fan?.Duty ?? double.NaN;
        }
    }
}
