using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.App.ViewModels;

/// <summary>The battery's charge limit, calibration and wear, and power-off USB charging.</summary>
public sealed partial class BatteryViewModel : ObservableObject
{
    private readonly DeviceSession _session;
    private readonly NotificationService _notifications;
    private bool _loading;
    private bool _firmwareFeatures;
    private int _healthRequest;

    public BatteryViewModel(DispatcherQueue dispatcher, DeviceSession session, NotificationService notifications)
    {
        _session = session;
        _notifications = notifications;
        session.TelemetryUpdated += telemetry => dispatcher.TryEnqueue(() => ShowCalibration(telemetry.Battery));
    }

    /// <summary>The battery levels USB charging can stop at, as the firmware takes them.</summary>
    public IReadOnlyList<string> UsbFloors { get; } =
        [.. AcerProtocol.UsbChargingFloors.Select(floor => Strings.Format("UsbCharging_Floor", floor))];

    [ObservableProperty]
    public partial bool Available { get; set; }

    [ObservableProperty]
    public partial bool ChargeLimitAvailable { get; set; }

    [ObservableProperty]
    public partial bool ChargeLimit { get; set; }

    [ObservableProperty]
    public partial bool UsbChargingAvailable { get; set; }

    [ObservableProperty]
    public partial bool UsbCharging { get; set; }

    [ObservableProperty]
    public partial int UsbFloorIndex { get; set; }

    [ObservableProperty]
    public partial bool CalibrationAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotCalibrating))]
    public partial bool Calibrating { get; set; }

    public bool NotCalibrating => !Calibrating;

    [ObservableProperty]
    public partial string CalibrationStatus { get; set; } = "";

    /// <summary>The battery reports its wear (every laptop battery Windows knows does).</summary>
    [ObservableProperty]
    public partial bool HealthAvailable { get; set; }

    /// <summary>E.g. "64 % of its original capacity (37.9 of 58.8 Wh)".</summary>
    [ObservableProperty]
    public partial string HealthSummary { get; set; } = "";

    /// <summary>E.g. "74 charge cycles"; empty when the battery doesn't count them.</summary>
    [ObservableProperty]
    public partial string HealthCycles { get; set; } = "";

    /// <summary>The battery's maker and model, e.g. "SMP AP18E7M".</summary>
    [ObservableProperty]
    public partial string HealthBattery { get; set; } = "";

    /// <summary>Asked before a calibration starts; true to go ahead.</summary>
    public Func<Task<bool>>? ConfirmCalibration { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        var caps = _session.Capabilities;
        var power = _session.Settings.Power;
        var firmware = _session.Firmware;

        ChargeLimitAvailable = caps.Battery.ChargeLimit;
        ChargeLimit = power.ChargeLimit ?? firmware?.ChargeLimit ?? false;

        UsbChargingAvailable = caps.UsbCharging;
        var usb = power.UsbCharging
            ?? (firmware?.UsbCharging is { } state ? new UsbChargingSettings(state.On, state.Floor ?? AcerProtocol.DefaultUsbChargingFloor) : null);
        UsbCharging = usb?.On ?? false;
        UsbFloorIndex = FloorIndex(usb?.Floor ?? AcerProtocol.DefaultUsbChargingFloor);

        CalibrationAvailable = caps.Battery.Calibration;
        _firmwareFeatures = ChargeLimitAvailable || UsbChargingAvailable || CalibrationAvailable;
        Available = _firmwareFeatures || HealthAvailable;
        ShowCalibration(_session.Latest?.Battery);
        _loading = false;
        _ = LoadHealthAsync();
    }

    private async Task LoadHealthAsync()
    {
        var request = ++_healthRequest;
        var health = await _session.ReadBatteryHealthAsync();
        if (request != _healthRequest)
            return;
        HealthAvailable = health is { DesignCapacity: > 0 };
        Available = _firmwareFeatures || HealthAvailable;
        if (health is not { DesignCapacity: > 0 })
            return;
        HealthSummary = health.Relative
            ? Strings.Format("BatteryHealth_CapacityRelative", health.Percent)
            : Strings.Format("BatteryHealth_Capacity", health.Percent, WattHours(health.FullChargeCapacity), WattHours(health.DesignCapacity));
        HealthCycles = health.CycleCount is { } cycles ? Strings.Format("BatteryHealth_Cycles", cycles) : "";
        HealthBattery = string.Join(" ", new[] { health.Manufacturer, health.Name }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string WattHours(int milliwattHours) => (milliwattHours / 1000.0).ToString("0.0", CultureInfo.CurrentCulture);

    partial void OnChargeLimitChanged(bool value)
    {
        if (!_loading)
            ApplyPower(p => p with { ChargeLimit = value });
    }

    partial void OnUsbChargingChanged(bool value) => ApplyUsbCharging();

    partial void OnUsbFloorIndexChanged(int value) => ApplyUsbCharging();

    private void ApplyUsbCharging()
    {
        // Selection controls briefly report -1 while (re)initialising.
        if (_loading || UsbFloorIndex < 0 || UsbFloorIndex >= AcerProtocol.UsbChargingFloors.Count)
            return;
        ApplyPower(p => p with { UsbCharging = new UsbChargingSettings(UsbCharging, AcerProtocol.UsbChargingFloors[UsbFloorIndex]) });
    }

    private void ApplyPower(Func<PowerSettings, PowerSettings> change) => _ = _session.SetPowerAsync(change(_session.Settings.Power));

    [RelayCommand]
    private async Task StartCalibrationAsync()
    {
        if (ConfirmCalibration is { } confirm && !await confirm())
            return;
        var result = await _session.StartBatteryCalibrationAsync();
        var problem = result switch
        {
            CalibrationResult.Started => null,
            CalibrationResult.NeedsAc => Strings.Get("Calibration_NeedsAc"),
            CalibrationResult.AlreadyRunning => Strings.Get("Calibration_AlreadyRunning"),
            _ => Strings.Get("Calibration_Rejected"),
        };
        if (problem is not null)
            _notifications.Show(Strings.Get("Notice_Battery_Title"), problem, InfoBarSeverity.Warning);
    }

    [RelayCommand]
    private Task StopCalibrationAsync() => _session.StopBatteryCalibrationAsync();

    private void ShowCalibration(BatteryTelemetry? battery)
    {
        Calibrating = battery?.CalibrationStarted is not null;
        if (battery?.CalibrationStarted is not { } started)
        {
            CalibrationStatus = "";
            return;
        }
        var since = started.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        CalibrationStatus = (battery.Charging, battery.Percent) switch
        {
            (true, { } percent) => Strings.Format("Calibration_Charging", since, percent),
            (false, { } percent) => Strings.Format("Calibration_Discharging", since, percent),
            _ => Strings.Format("Calibration_Running", since),
        };
    }

    private static int FloorIndex(int floor) => Math.Max(0, AcerProtocol.UsbChargingFloors.ToList().IndexOf(floor));
}
