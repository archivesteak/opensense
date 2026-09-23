using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OpenSense.App.Services;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.App.ViewModels;

/// <summary>Keyboard and display switches, power plans and the GPU (MUX) switch.</summary>
public sealed partial class SystemViewModel(DispatcherQueue dispatcher, DeviceSession session, NotificationService notifications)
    : ObservableObject
{
    private bool _loading;

    public ObservableCollection<PowerPlan> PowerPlans { get; } = [];

    [ObservableProperty]
    public partial bool BacklightAutoOffAvailable { get; set; }

    [ObservableProperty]
    public partial bool BacklightAutoOff { get; set; }

    [ObservableProperty]
    public partial bool WindowsKeyAvailable { get; set; }

    [ObservableProperty]
    public partial bool WindowsKey { get; set; } = true;

    [ObservableProperty]
    public partial bool LcdOverdriveAvailable { get; set; }

    [ObservableProperty]
    public partial bool LcdOverdrive { get; set; }

    [ObservableProperty]
    public partial bool StickyKeysOn { get; set; }

    [ObservableProperty]
    public partial bool PowerPlansAvailable { get; set; }

    [ObservableProperty]
    public partial PowerPlan? SelectedPowerPlan { get; set; }

    [ObservableProperty]
    public partial bool GpuSwitchAvailable { get; set; }

    /// <summary>0 = hybrid (Optimus), 1 = discrete only.</summary>
    [ObservableProperty]
    public partial int GpuModeIndex { get; set; }

    [ObservableProperty]
    public partial bool RestartPending { get; set; }

    public bool AnyKeyboardSetting => BacklightAutoOffAvailable || WindowsKeyAvailable;

    /// <summary>Asked before a GPU mode switch; returns true to restart now, false for later, null to cancel.</summary>
    public Func<GpuMode, Task<bool?>>? ConfirmGpuSwitch { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        var caps = session.Capabilities;
        var kb = session.Settings.Keyboard;
        var state = session.InitialKeyboard;

        BacklightAutoOffAvailable = caps.Keyboard.BacklightAutoOff;
        BacklightAutoOff = kb.BacklightAutoOff ?? state?.BacklightAutoOff ?? false;
        WindowsKeyAvailable = caps.Keyboard.WindowsKey;
        WindowsKey = kb.WindowsKey ?? state?.WindowsKey ?? true;
        LcdOverdriveAvailable = caps.Keyboard.LcdOverdrive;
        LcdOverdrive = kb.LcdOverdrive ?? state?.LcdOverdrive ?? false;
        StickyKeysOn = StickyKeys.IsEnabled() ?? false;
        OnPropertyChanged(nameof(AnyKeyboardSetting));

        PowerPlansAvailable = caps.PowerPlans;
        PowerPlans.Clear();
        if (caps.PowerPlans)
        {
            foreach (var plan in Core.Hardware.PowerPlans.List())
                PowerPlans.Add(plan);
            var active = Core.Hardware.PowerPlans.Active();
            SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Id == active);
        }

        GpuSwitchAvailable = caps.GpuModeSwitch;
        _firmwareGpuIndex = session.Firmware?.GpuMode == GpuMode.Discrete ? 1 : 0;
        GpuModeIndex = _firmwareGpuIndex;
        _loading = false;
    }

    partial void OnBacklightAutoOffChanged(bool value) => ApplyKeyboard(k => k with { BacklightAutoOff = value });

    partial void OnWindowsKeyChanged(bool value) => ApplyKeyboard(k => k with { WindowsKey = value });

    partial void OnLcdOverdriveChanged(bool value) => ApplyKeyboard(k => k with { LcdOverdrive = value });

    partial void OnStickyKeysOnChanged(bool value)
    {
        // A per-user Windows setting, so the app sets it itself (the service runs as SYSTEM).
        if (!_loading)
            _ = Task.Run(() => StickyKeys.SetEnabled(value));
    }

    partial void OnSelectedPowerPlanChanged(PowerPlan? value)
    {
        if (!_loading && value is not null && value.Id != Core.Hardware.PowerPlans.Active() && !Core.Hardware.PowerPlans.SetActive(value.Id))
            notifications.Show("Power plan", $"Windows did not switch to \"{value.Name}\".");
    }

    /// <summary>GPU mode the firmware is set to (0 hybrid, 1 discrete).</summary>
    private int _firmwareGpuIndex;

    partial void OnGpuModeIndexChanged(int oldValue, int newValue)
    {
        // Selection controls briefly report -1 while (re)initialising; only a real change asks the user.
        if (_loading || newValue is < 0 or > 1 || newValue == _firmwareGpuIndex)
            return;
        _ = SwitchGpuModeAsync(_firmwareGpuIndex, newValue);
    }

    [RelayCommand]
    private static void RestartNow()
    {
        WindowsRestart.RestartNow();
    }

    private void ApplyKeyboard(Func<KeyboardSettings, KeyboardSettings> change)
    {
        if (_loading)
            return;
        _ = session.SetKeyboardAsync(change(session.Settings.Keyboard));
    }

    private async Task SwitchGpuModeAsync(int oldIndex, int newIndex)
    {
        var mode = newIndex == 1 ? GpuMode.Discrete : GpuMode.Hybrid;
        var decision = ConfirmGpuSwitch is null ? false : await ConfirmGpuSwitch(mode);
        if (decision is null)
        {
            Revert(oldIndex);
            return;
        }

        var ok = await session.SetGpuModeAsync(mode);
        if (!ok)
        {
            notifications.Show("GPU mode", "The firmware rejected the GPU mode change.", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
            Revert(oldIndex);
            return;
        }

        _firmwareGpuIndex = newIndex;
        RestartPending = true;
        if (decision == true)
            WindowsRestart.RestartNow();
    }

    private void Revert(int index) => dispatcher.TryEnqueue(() =>
    {
        _loading = true;
        GpuModeIndex = index;
        _loading = false;
    });
}
