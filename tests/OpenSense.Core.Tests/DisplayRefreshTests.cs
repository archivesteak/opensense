 using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Tests;

public class DisplayRefreshTests
{
    private sealed class FakePowerSource(bool initialOnAc) : IPowerSource
    {
        public bool OnAc { get; set; } = initialOnAc;
        public PowerStatus Read() => new(OnAc, BatteryPercent: 80, Charging: OnAc);
    }

    private sealed class FakeDisplayBackend(params DisplayRefreshInfo[] initialDisplays) : IDisplayRefreshBackend
    {
        private readonly Dictionary<string, DisplayRefreshInfo> _displays = initialDisplays.ToDictionary(d => d.DeviceName);

        public List<(string DeviceName, uint TargetHz)> Calls { get; } = [];
        public bool FailWrites { get; set; }

        public IReadOnlyList<DisplayRefreshInfo> GetDisplays() => [.. _displays.Values];

        public bool SetRefreshRate(string deviceName, uint refreshRateHz)
        {
            Calls.Add((deviceName, refreshRateHz));
            if (FailWrites || !_displays.TryGetValue(deviceName, out var info))
                return false;
            _displays[deviceName] = info with { CurrentHz = refreshRateHz };
            return true;
        }

        public void SetCurrentRate(string deviceName, uint currentHz) =>
            _displays[deviceName] = _displays[deviceName] with { CurrentHz = currentHz };
    }

    [Theory]
    [InlineData(new uint[] { 60, 144 }, false, 60u)]
    [InlineData(new uint[] { 60, 144 }, true, 144u)]
    [InlineData(new uint[] { 59, 120, 240 }, false, 59u)]
    [InlineData(new uint[] { 59, 120, 240 }, true, 240u)]
    [InlineData(new uint[] { 60 }, true, 60u)]
    [InlineData(new uint[] { 48, 120 }, false, null)]
    [InlineData(new uint[] { }, false, null)]
    public void Policy_chooses_60hz_on_battery_and_highest_on_ac(uint[] rates, bool onAc, uint? expected)
    {
        Assert.Equal(expected, DisplayRefreshPolicy.ChooseTargetFrequency(rates, onAc));
    }

    [Fact]
    public void Policy_reports_whether_switching_is_possible()
    {
        Assert.True(DisplayRefreshPolicy.CanSwitchBetweenBatteryAndAc([60u, 144u]));
        Assert.True(DisplayRefreshPolicy.CanSwitchBetweenBatteryAndAc([59u, 165u]));
        Assert.False(DisplayRefreshPolicy.CanSwitchBetweenBatteryAndAc([60u]));
        Assert.False(DisplayRefreshPolicy.CanSwitchBetweenBatteryAndAc([48u, 120u]));
    }

    [Fact]
    public void Controller_sets_60hz_on_battery_and_rolls_back_to_highest_on_ac()
    {
        var power = new FakePowerSource(initialOnAc: true);
        var backend = new FakeDisplayBackend(new DisplayRefreshInfo(@"\\.\DISPLAY1", 144, [60, 144]));
        var controller = new DisplayRefreshController(backend, power);

        var initial = controller.SetEnabled(true);
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, true, null), initial);
        Assert.Empty(backend.Calls); // already at highest rate on AC

        power.OnAc = false;
        Assert.True(controller.NeedsReconcile(onAc: false));
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, false, 60u), controller.Reconcile());
        Assert.Equal((@"\\.\DISPLAY1", 60u), Assert.Single(backend.Calls));
        Assert.False(controller.NeedsReconcile(onAc: false));

        // Manual override while staying on battery is respected until power state changes.
        backend.SetCurrentRate(@"\\.\DISPLAY1", 120);
        Assert.False(controller.NeedsReconcile(onAc: false));
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Skipped, false, null), controller.Reconcile());
        Assert.Single(backend.Calls);

        power.OnAc = true;
        Assert.True(controller.NeedsReconcile(onAc: true));
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, true, 144u), controller.Reconcile());
        Assert.Equal(2, backend.Calls.Count);
        Assert.Equal((@"\\.\DISPLAY1", 144u), backend.Calls[1]);
        Assert.False(controller.NeedsReconcile(onAc: true));
    }

    [Fact]
    public void Controller_applies_immediately_when_starting_on_battery()
    {
        var power = new FakePowerSource(initialOnAc: false);
        var backend = new FakeDisplayBackend(new DisplayRefreshInfo(@"\\.\DISPLAY1", 165, [60, 120, 165]));
        var controller = new DisplayRefreshController(backend, power);

        var result = controller.SetEnabled(true);

        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, false, 60u), result);
        Assert.Equal((@"\\.\DISPLAY1", 60u), Assert.Single(backend.Calls));
        Assert.Equal(60u, backend.GetDisplays()[0].CurrentHz);
    }

    [Fact]
    public void Controller_ignores_fixed_60hz_external_monitor_while_switching_laptop_display()
    {
        var power = new FakePowerSource(initialOnAc: false);
        var backend = new FakeDisplayBackend(
            new DisplayRefreshInfo(@"\\.\DISPLAY1", 165, [60, 165]),
            new DisplayRefreshInfo(@"\\.\DISPLAY2", 60, [60]));
        var controller = new DisplayRefreshController(backend, power);

        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, false, 60u), controller.SetEnabled(true));
        Assert.Equal((@"\\.\DISPLAY1", 60u), Assert.Single(backend.Calls));

        power.OnAc = true;
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, true, 165u), controller.Reconcile());
        Assert.Equal(2, backend.Calls.Count);
        Assert.Equal((@"\\.\DISPLAY1", 165u), backend.Calls[1]);
    }

    [Fact]
    public void Controller_bounds_consecutive_failures_and_recovers_after_unlock_or_resume()
    {
        var power = new FakePowerSource(initialOnAc: false);
        var backend = new FakeDisplayBackend(new DisplayRefreshInfo(@"\\.\DISPLAY1", 144, [60, 144])) { FailWrites = true };
        var controller = new DisplayRefreshController(backend, power);

        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Failed, false, null), controller.SetEnabled(true));
        Assert.Null(controller.AppliedOnAc);
        for (var i = 1; i < DisplayRefreshController.MaxConsecutiveFailures; i++)
        {
            Assert.True(controller.NeedsReconcile(onAc: false));
            Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Failed, false, null), controller.Reconcile());
        }

        Assert.False(controller.NeedsReconcile(onAc: false));
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Skipped, false, null), controller.Reconcile());

        backend.FailWrites = false;
        Assert.True(controller.OnDesktopUnlocked());
        Assert.True(controller.NeedsReconcile(onAc: false));
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, false, 60u), controller.Reconcile());
        Assert.Equal(60u, backend.GetDisplays()[0].CurrentHz);

        // Sleep while on battery, plug in while asleep, wake up on AC.
        power.OnAc = true;
        Assert.True(controller.OnPowerResume());
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Applied, true, 144u), controller.Reconcile());
        Assert.Equal(144u, backend.GetDisplays()[0].CurrentHz);
    }

    [Fact]
    public void Controller_does_not_touch_display_when_disabled()
    {
        var power = new FakePowerSource(initialOnAc: false);
        var backend = new FakeDisplayBackend(new DisplayRefreshInfo(@"\\.\DISPLAY1", 144, [60, 144]));
        var controller = new DisplayRefreshController(backend, power);

        Assert.False(controller.NeedsReconcile(onAc: false));
        Assert.Equal(new DisplayRefreshResult(DisplayRefreshOutcome.Skipped, false, null), controller.Reconcile());
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public void Ui_settings_default_refresh_rate_on_battery_to_true_and_round_trip_false()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opensense-refresh-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore<UserSettings>(path);
            Assert.True(store.Load().Ui.RefreshRateOnBattery);

            store.Save(new UserSettings { Ui = new UiSettings { RefreshRateOnBattery = false } });
            Assert.False(store.Load().Ui.RefreshRateOnBattery);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
