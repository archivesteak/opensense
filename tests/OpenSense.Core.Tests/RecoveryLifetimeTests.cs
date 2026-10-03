using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Tests;

public class RecoveryLifetimeTests
{
    private static FanControlService Controller(FakeFirmware firmware, DirectSensors? sensors = null, Func<DateTime>? clock = null)
    {
        var device = new AcerDevice(firmware);
        return new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), new FakePower(),
            new ControlProfile { Mode = FanControlMode.Custom }, sensors, clock);
    }

    [Fact]
    public async Task Stop_rejects_pending_requests_and_retains_the_running_owner_until_completion()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var controller = Controller(new FakeFirmware());
        controller.Start();
        var running = controller.InvokeAsync(_ =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
            return 7;
        });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            var pending = controller.InvokeAsync<int>(_ => throw new InvalidOperationException("Queued work must not execute."));
            var stopped = controller.StopAsync();
            Assert.False(stopped.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.InvokeAsync(_ => 1));
            release.Set();
            Assert.Equal(7, await running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            await stopped.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            controller.Update(new ControlProfile()); // Late wake requests cannot touch a disposed event.
            controller.OnResume();
            controller.Dispose();
        }
        finally
        {
            release.Set();
            await controller.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Lighting_worker_drains_in_flight_and_queued_work_before_completing_stop()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = new LightingWorker();
        var first = worker.InvokeAsync(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
            return 1;
        });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            var second = worker.InvokeAsync(() => 2);
            var stopped = worker.StopAsync();
            Assert.False(stopped.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.InvokeAsync(() => 3));
            release.Set();
            Assert.Equal(1, await first.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            Assert.Equal(2, await second.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            await stopped.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            worker.Dispose();
        }
        finally
        {
            release.Set();
            await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_dispatcher_exception_does_not_wedge_keyboard_or_power_pumps()
    {
        using var laptop = new SimulatedTransport(SimulatedModel.Nitro2021);
        var keyboardDevice = new AcerDevice(laptop);
        var keyboard = new KeyboardService(new FailOnceDispatcher(keyboardDevice), new KeyboardCapabilities { WindowsKey = true });
        var keyboardNotices = new List<NoticeKind>();
        keyboard.Notice += n => keyboardNotices.Add(n.Kind);
        var keyboardSettings = new KeyboardSettings { WindowsKey = false };
        await keyboard.ApplyAsync(keyboardSettings);
        await keyboard.ApplyAsync(keyboardSettings);
        Assert.Contains(NoticeKind.ControlLoopError, keyboardNotices);
        Assert.False(KeyboardState.Read(keyboardDevice, new KeyboardCapabilities { WindowsKey = true }).WindowsKey);
        await keyboard.StopAsync();

        var firmware = new FakeFirmware { SupportsUsbCharging = true };
        using var power = new PowerService(new FailOnceDispatcher(new AcerDevice(firmware)),
            DeviceCapabilities.None with { UsbCharging = true }, new FakePower(), new FakeSystemPower(), null, _ => { });
        var powerNotices = new List<NoticeKind>();
        power.Notice += n => powerNotices.Add(n.Kind);
        var settings = new PowerSettings { UsbCharging = new UsbChargingSettings(true, 20) };
        await power.ApplyAsync(settings);
        await power.ApplyAsync(settings);
        Assert.Contains(NoticeKind.ControlLoopError, powerNotices);
        Assert.Equal(0x140F00UL, firmware.UsbCharging);
    }

    [Fact]
    public async Task Stop_cancels_old_resume_delays_and_does_not_reapply_later()
    {
        var time = new FakeTimeProvider();
        using var laptop = new SimulatedTransport(SimulatedModel.Nitro2021);
        var dispatcher = new CountingDispatcher(new AcerDevice(laptop));
        var keyboard = new KeyboardService(dispatcher, new KeyboardCapabilities { WindowsKey = true }, time: time);
        var backend = new BlockingReadBackend();
        var lighting = new LightingService([backend], time);
        using var power = new PowerService(dispatcher, DeviceCapabilities.None, new FakePower(), new FakeSystemPower(), null, _ => { }, time);
        var resumed = new[] { keyboard.OnResume(), lighting.OnResume(), power.OnResume() };
        await Task.WhenAll(keyboard.StopAsync(), lighting.StopAsync(), power.StopAsync());
        time.Advance(TimeSpan.FromSeconds(30));
        foreach (var resume in resumed)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resume);
        Assert.Equal(0, dispatcher.Calls);
        Assert.Equal(0, backend.Applies);
    }

    [Fact]
    public async Task Lighting_stop_waits_for_reads_before_their_devices_can_be_replaced()
    {
        var backend = new BlockingReadBackend();
        var lighting = new LightingService([backend]);
        var read = lighting.ReadAsync();
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var stopped = lighting.StopAsync();
        Assert.False(stopped.IsCompleted);
        backend.Release.SetResult(new LightingSettings());
        Assert.Single(await read);
        await stopped.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => lighting.ReadAsync());
    }

    [Theory]
    [InlineData(207)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void Invalid_direct_temperatures_fall_back_without_aborting_control(double bad)
    {
        using var sensors = new DirectSensors(new TestSensor(() => bad), new TestSensor(() => throw new InvalidOperationException("GPU read failed")),
            new ThrowingGpuPower(), new SensorStatus(ChipSensor.AmdTctl), new SensorStatus(ChipSensor.GraphicsDriver));
        using var controller = Controller(new FakeFirmware(), sensors);
        controller.Tick();
        Assert.Equal(60, controller.Latest!.CpuTemperature);
        Assert.Equal(55, controller.Latest.GpuTemperature);
        Assert.Equal(TemperatureOrigin.Firmware, controller.Latest.CpuTemperatureOrigin);
        Assert.Equal(TemperatureOrigin.Firmware, controller.Latest.GpuTemperatureOrigin);
        Assert.Equal(SensorProblem.NoReading, sensors.CpuStatus.Problem);
        Assert.Equal(SensorProblem.NoReading, sensors.GpuStatus.Problem);
        Assert.False(controller.Latest.Failsafe);
    }

    [Fact]
    public void A_register_read_of_all_ones_is_the_207_degrees_the_gate_discards()
    {
        // What the AMD sensor made of a failed read: the spikes to 207 °C that sent the fans to the top of their curve (issue 3).
        Assert.Equal(206.875, AmdTctlSensor.Decode(0xFFFFFFFF));
        Assert.Null(SensorReadings.Temperature(AmdTctlSensor.Decode(0xFFFFFFFF)));
        Assert.Equal(66.5, AmdTctlSensor.Decode(532u << 21)); // 532 eighths of a degree, no offset bits
    }

    [Fact]
    public void Invalid_firmware_cpu_temperature_still_enters_the_existing_auto_failsafe()
    {
        var now = DateTime.UtcNow;
        using var controller = Controller(new FakeFirmware { CpuTemp = 207, GpuTemp = 207 }, clock: () => now);
        for (var i = 0; i < 3; i++)
        {
            controller.Tick();
            now += TimeSpan.FromSeconds(1);
        }
        Assert.Null(controller.Latest!.CpuTemperature);
        Assert.Null(controller.Latest.GpuTemperature);
        Assert.True(controller.Latest.Failsafe);
        Assert.Equal(FanControlMode.Auto, controller.Latest.EffectiveMode);
        Assert.False(controller.Latest.GpuAsleep); // Invalid GPU data is not evidence of a sleeping GPU.
    }

    [Fact]
    public void A_disposed_HID_channel_cannot_reopen_even_if_the_interface_is_present()
    {
        var device = new FakeHidDevice(FakeKyd100.Descriptor);
        var bus = new CountingHidBus(device);
        var channel = HidChannel.Open(bus, _ => true)!;
        Assert.Equal(1, bus.Opens);
        channel.Dispose();
        Assert.False(channel.SetFeature([1, 2]));
        Assert.False(channel.GetFeature([1, 0]));
        Assert.Equal(1, bus.Opens);
        channel.Dispose();
    }

    [Fact]
    public async Task Optional_monitoring_and_HID_factory_failures_keep_the_engine_ready_with_firmware_fallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense-recovery-" + Guid.NewGuid().ToString("N"));
        var machine = new RecoveryMachine { FailOptionalFactories = true };
        using var engine = new OpenSenseEngine(machine, Path.Combine(directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        engine.Start();
        try
        {
            var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
            Assert.Equal(EngineState.Ready, snapshot.State);
            Assert.Equal(SensorProblem.NoReading, snapshot.TemperatureSources.Cpu.Problem);
            Assert.Null(snapshot.CpuName);
            Assert.NotEmpty(snapshot.Capabilities.ControllableFans);
        }
        finally
        {
            engine.Dispose();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Failed_start_after_control_begins_rolls_back_opened_resources_without_waiting_for_Dispose()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense-recovery-" + Guid.NewGuid().ToString("N"));
        var machine = new RecoveryMachine { FailSystemPower = true };
        using var engine = new OpenSenseEngine(machine, Path.Combine(directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        engine.Start();
        try
        {
            Assert.Equal(EngineState.Failed, engine.State);
            await machine.FirmwareDisposed.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.True(machine.LoadDisposed);
            Assert.True(machine.HidDisposed);
        }
        finally
        {
            engine.Dispose();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task HID_enumeration_failure_keeps_the_arrival_pending_and_retries_without_another_event()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense-recovery-" + Guid.NewGuid().ToString("N"));
        var hid = new RetryingHidBus();
        var machine = new RecoveryMachine { HidOverride = hid };
        using var engine = new OpenSenseEngine(machine, Path.Combine(directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        var rebuilt = new TaskCompletionSource<EngineSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Rebuilt += (_, snapshot) => rebuilt.TrySetResult(snapshot);
        engine.Start();
        try
        {
            Assert.Equal(EngineState.Ready, engine.State);
            hid.RaiseWithNextEnumerationFailure();
            await hid.Failed.Task.WaitAsync(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
            var recovered = await rebuilt.Task.WaitAsync(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
            Assert.Equal(EngineState.Ready, recovered.State);
            Assert.Single(recovered.Capabilities.Lights, l => l.Source is not null);
        }
        finally
        {
            engine.Dispose();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private sealed class FailOnceDispatcher(AcerDevice device) : IDeviceDispatcher
    {
        private bool _failed;
        public Task<T> InvokeAsync<T>(Func<AcerDevice, T> action)
        {
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException("A transient dispatcher fault.");
            }
            return Task.FromResult(action(device));
        }
    }

    private sealed class CountingDispatcher(AcerDevice device) : IDeviceDispatcher
    {
        public int Calls { get; private set; }
        public Task<T> InvokeAsync<T>(Func<AcerDevice, T> action)
        {
            Calls++;
            return Task.FromResult(action(device));
        }
    }

    private sealed class BlockingReadBackend : ILightingBackend
    {
        public LightingDeviceInfo Device { get; } = new("test", LightingLocation.Keyboard, LightingBackendKind.Kyd100);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<LightingSettings?> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Applies { get; private set; }
        public Task<bool> ApplyAsync(LightingSettings settings)
        {
            Applies++;
            return Task.FromResult(true);
        }
        public Task<LightingSettings?> ReadAsync()
        {
            Entered.TrySetResult();
            return Release.Task;
        }
    }

    private sealed class TestSensor(Func<double?> read) : ITemperatureSensor
    {
        public SensorStatus Status { get; } = new(ChipSensor.AmdTctl);
        public double? Read() => read();
        public void Dispose() { }
    }

    private sealed class ThrowingGpuPower : IGpuPowerState
    {
        public bool? IsOn() => throw new InvalidOperationException("Power query failed.");
    }

    private sealed class CountingHidBus(IHidDevice device) : IHidBus
    {
        public int Opens { get; private set; }
        public IReadOnlyList<HidDeviceInfo> Enumerate() => [device.Info];
        public IHidDevice? Open(HidDeviceInfo info)
        {
            Opens++;
            return device;
        }
    }

    private sealed class RecoveryMachine : IMachine
    {
        private readonly SimulatedMachine _inner = new(SimulatedModel.Nitro2021) { ModelName = "Nitro ANV16-41" };
        public bool FailOptionalFactories { get; init; }
        public bool FailSystemPower { get; init; }
        public IHidBus? HidOverride { get; init; }
        public TaskCompletionSource FirmwareDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool LoadDisposed { get; private set; }
        public bool HidDisposed { get; private set; }
        public string? Model => _inner.Model;
        public string? BiosVersion => _inner.BiosVersion;
        public string? SerialNumber => _inner.SerialNumber;
        public IWmiTransport OpenFirmware() => new TrackedFirmware(_inner.OpenFirmware(), () => FirmwareDisposed.TrySetResult());
        public IFirmwareEvents OpenFirmwareEvents() => _inner.OpenFirmwareEvents();
        public IPowerSource OpenPowerSource() => _inner.OpenPowerSource();
        public ISystemPower OpenSystemPower() => FailSystemPower ? throw new InvalidOperationException("System power failed.") : _inner.OpenSystemPower();
        public BatteryHealth? ReadBatteryHealth() => _inner.ReadBatteryHealth();
        public IHidBus OpenHid() => FailOptionalFactories ? throw new InvalidOperationException("HID failed.") : HidOverride ?? new TrackedHid(() => HidDisposed = true);
        public AcerSmbios ReadSmbios() => _inner.ReadSmbios();
        public ILoadMonitor OpenLoadMonitor() => FailOptionalFactories ? throw new InvalidOperationException("Counters failed.") : new TrackedLoad(() => LoadDisposed = true);
        public DirectSensors OpenSensors() => FailOptionalFactories ? throw new InvalidOperationException("Direct sensors failed.") : DirectSensors.None;
        public IBootLogoStore? OpenBootLogo(Action<string> log) => null;
        public PixelSize? ReadInternalScreen() => _inner.ReadInternalScreen();
    }

    private sealed class TrackedLoad(Action disposed) : ILoadMonitor
    {
        public string? CpuName => "simulated";
        public string? GpuName => "simulated";
        public (double? Cpu, double? Gpu) Sample() => (10, 5);
        public void Dispose() => disposed();
    }

    private sealed class TrackedHid(Action disposed) : IHidBus, IDisposable
    {
        public IReadOnlyList<HidDeviceInfo> Enumerate() => [];
        public IHidDevice? Open(HidDeviceInfo device) => null;
        public void Dispose() => disposed();
    }

    private sealed class RetryingHidBus : IHidBus, IDisposable
    {
        private readonly FakeKyd100 _device = new();
        private int _failNext;
        public RetryingHidBus() => _device.Lights[(byte)Kyd100Light.Keyboard] = (4, 0);
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<string>? Changed;
        public IReadOnlyList<HidDeviceInfo> Enumerate()
        {
            if (Interlocked.Exchange(ref _failNext, 0) == 1)
            {
                Failed.TrySetResult();
                throw new InvalidOperationException("Transient enumeration failure.");
            }
            return [_device.Info];
        }
        public IHidDevice? Open(HidDeviceInfo device) => _device;
        public void Dispose() => _device.Dispose();
        public void RaiseWithNextEnumerationFailure()
        {
            Interlocked.Exchange(ref _failNext, 1);
            Changed?.Invoke(_device.Info.Path);
        }
    }

    private sealed class TrackedFirmware(IWmiTransport inner, Action disposed) : IWmiTransport
    {
        public bool IsClassAvailable(string className) => inner.IsClassAvailable(className);
        public ulong Invoke(string className, string method, ulong input) => inner.Invoke(className, method, input);
        public WmiArrayResult InvokeArray(string className, string method, byte[]? input) => inner.InvokeArray(className, method, input);
        public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs) => inner.InvokeNamed(className, method, inputs);
        public IReadOnlyList<string> ReadStrings(string className, string propertyName) => inner.ReadStrings(className, propertyName);
        public bool HasArrayInput(string className, string method) => inner.HasArrayInput(className, method);
        public void Dispose()
        {
            inner.Dispose();
            disposed();
        }
    }
}
