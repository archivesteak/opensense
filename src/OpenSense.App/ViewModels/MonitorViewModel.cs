using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using OpenSense.App.Helpers;
using OpenSense.App.Services;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.App.ViewModels;

public sealed partial class FanReadingViewModel(FanId id, string name) : ObservableObject
{
    public FanId Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial int? Rpm { get; set; }

    [ObservableProperty]
    public partial int? Duty { get; set; }

    [ObservableProperty]
    public partial string BehaviorText { get; set; } = "Auto";

    public HistoryBuffer RpmHistory { get; } = new(MonitorViewModel.HistoryLength);

    public string RpmText => Units.Rpm(Rpm);

    public string DutyText => Duty is { } d ? $"{d}%" : "--";

    partial void OnRpmChanged(int? value) => OnPropertyChanged(nameof(RpmText));

    partial void OnDutyChanged(int? value) => OnPropertyChanged(nameof(DutyText));
}

/// <summary>Live sensor readings for the dashboard, tray and graphs.</summary>
public sealed partial class MonitorViewModel : ObservableObject
{
    /// <summary>Five minutes at one sample per second.</summary>
    public const int HistoryLength = 300;

    private readonly DispatcherQueue _dispatcher;
    private readonly SettingsService _settings;
    private readonly DeviceSession _session;

    public MonitorViewModel(DispatcherQueue dispatcher, SettingsService settings, DeviceSession session)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _session = session;
        UseFahrenheit = settings.Current.Ui.UseFahrenheit;
        settings.Changed += s => _dispatcher.TryEnqueue(() => UseFahrenheit = s.Ui.UseFahrenheit);
        session.TelemetryUpdated += telemetry => _dispatcher.TryEnqueue(() => Apply(telemetry));
    }

    public ObservableCollection<FanReadingViewModel> Fans { get; } = [];

    public HistoryBuffer CpuHistory { get; } = new(HistoryLength);

    public HistoryBuffer GpuHistory { get; } = new(HistoryLength);

    public HistoryBuffer CpuLoadHistory { get; } = new(HistoryLength);

    public HistoryBuffer GpuLoadHistory { get; } = new(HistoryLength);

    /// <summary>Changes on every sample so graphs know to redraw.</summary>
    [ObservableProperty]
    public partial int Revision { get; set; }

    [ObservableProperty]
    public partial bool UseFahrenheit { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuTemperatureText), nameof(Hottest), nameof(CpuTemperatureValue))]
    public partial double? CpuTemperature { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuTemperatureText), nameof(Hottest), nameof(GpuTemperatureValue))]
    public partial double? GpuTemperature { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuTemperatureText))]
    public partial bool GpuAsleep { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SystemTemperatureText))]
    public partial double? SystemTemperature { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuLoadText))]
    public partial double? CpuLoad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuLoadText))]
    public partial double? GpuLoad { get; set; }

    [ObservableProperty]
    public partial Telemetry? Latest { get; set; }

    [ObservableProperty]
    public partial bool OnAcPower { get; set; } = true;

    [ObservableProperty]
    public partial bool HasGpu { get; set; }

    [ObservableProperty]
    public partial bool HasSystemTemperature { get; set; }

    [ObservableProperty]
    public partial FanReadingViewModel? CpuFan { get; set; }

    [ObservableProperty]
    public partial FanReadingViewModel? GpuFan { get; set; }

    [ObservableProperty]
    public partial string CpuName { get; set; } = "CPU";

    [ObservableProperty]
    public partial string GpuName { get; set; } = "GPU";

    public double? Hottest => Latest?.Hottest;

    /// <summary>For the temperature colour (NaN when unknown or asleep).</summary>
    public double CpuTemperatureValue => CpuTemperature ?? double.NaN;

    public double GpuTemperatureValue => GpuTemperature ?? double.NaN;

    public string CpuTemperatureText => Units.Temperature(CpuTemperature, UseFahrenheit);

    public string GpuTemperatureText => GpuAsleep ? "Idle" : Units.Temperature(GpuTemperature, UseFahrenheit);

    public string SystemTemperatureText => Units.Temperature(SystemTemperature, UseFahrenheit);

    public string CpuLoadText => Units.Percent(CpuLoad);

    public string GpuLoadText => Units.Percent(GpuLoad);

    public string UnitSymbol => UseFahrenheit ? "°F" : "°C";

    partial void OnUseFahrenheitChanged(bool value)
    {
        OnPropertyChanged(nameof(CpuTemperatureText));
        OnPropertyChanged(nameof(GpuTemperatureText));
        OnPropertyChanged(nameof(SystemTemperatureText));
        OnPropertyChanged(nameof(UnitSymbol));
    }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        var caps = _session.Capabilities;
        Fans.Clear();
        foreach (var fan in caps.Fans)
            Fans.Add(new FanReadingViewModel(fan.Id, fan.Name + " fan"));
        CpuFan = Fans.FirstOrDefault(f => f.Id == FanId.Cpu);
        GpuFan = Fans.FirstOrDefault(f => f.Id == FanId.Gpu);
        HasGpu = caps.Has(SensorId.GpuTemperature) || caps.Has(SensorId.GpuFanSpeed);
        HasSystemTemperature = caps.Has(SensorId.SystemTemperature);
        CpuName = _session.CpuName ?? "CPU";
        GpuName = _session.GpuName ?? "GPU";
        if (_session.Latest is { } latest)
            Apply(latest);
    }

    private void Apply(Telemetry t)
    {
        Latest = t;
        OnAcPower = t.OnAcPower;
        GpuAsleep = t.GpuAsleep;
        CpuLoad = t.CpuLoad;
        GpuLoad = t.GpuLoad;
        SystemTemperature = t.SystemTemperature;

        CpuTemperature = t.CpuTemperature;
        GpuTemperature = t.GpuTemperature;

        CpuHistory.Add(t.CpuTemperature);
        GpuHistory.Add(t.GpuTemperature);
        CpuLoadHistory.Add(t.CpuLoad);
        GpuLoadHistory.Add(t.GpuLoad);

        foreach (var fan in Fans)
        {
            var reading = t.Fan(fan.Id);
            fan.Rpm = reading?.Rpm;
            fan.Duty = reading?.Duty;
            fan.BehaviorText = (reading?.Behavior, t.EffectiveMode) switch
            {
                (FanBehavior.Max, _) when t.Emergency => "Emergency full speed",
                (FanBehavior.Max, _) => "Full speed",
                (FanBehavior.Custom, FanControlMode.Curve) => "Following curve",
                (FanBehavior.Custom, _) => "Fixed speed",
                _ => "Auto (firmware)",
            };
            fan.RpmHistory.Add(reading?.Rpm);
        }

        Revision++;
    }
}
