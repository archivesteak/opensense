using Microsoft.Extensions.Logging.Abstractions;
using Nerdbank.Streams;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Tests;

/// <summary>The engine behind the real JSON-RPC stack, over an in-memory duplex stream instead of the pipe.</summary>
public sealed class IpcTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
    private OpenSenseEngine _engine = null!;
    private StreamJsonRpc.JsonRpc _server = null!;
    private StreamJsonRpc.JsonRpc _client = null!;
    private IOpenSenseService _service = null!;

    private SimulatedMachine _machine = null!;

    public ValueTask InitializeAsync()
    {
        _machine = new SimulatedMachine(SimulatedModel.Nitro2022)
        {
            Smbios = new AcerSmbios(2, 0x56, [new AcerSmbiosRecord((byte)GamingRecord.CustomBootLogo, 1)], []),
            SystemPartition = Path.Combine(_directory, "esp"),
            GpuClocks = new FakeGpuClocks(),
            GpuLimit = 83,
        };
        _engine = new OpenSenseEngine(_machine, Path.Combine(_directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        _engine.Start();
        (_server, _client, _service) = Serve(_engine);
        return ValueTask.CompletedTask;
    }

    /// <summary>Serves <paramref name="engine"/> over an in-memory stream and connects a client to it.</summary>
    internal static (StreamJsonRpc.JsonRpc Server, StreamJsonRpc.JsonRpc Client, IOpenSenseService Service) Serve(OpenSenseEngine engine)
    {
        var (serverStream, clientStream) = FullDuplexStream.CreatePair();
        var server = OpenSensePipe.CreateRpc(serverStream);
        server.AddLocalRpcTarget<IOpenSenseService>(engine, new StreamJsonRpc.JsonRpcTargetOptions { NotifyClientOfEvents = true });
        server.StartListening();

        var client = OpenSensePipe.CreateRpc(clientStream);
        var service = client.Attach<IOpenSenseService>();
        client.StartListening();
        return (server, client, service);
    }

    [Fact]
    public async Task Snapshot_crosses_the_wire_intact()
    {
        var snapshot = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EngineState.Ready, snapshot.State);
        Assert.Equal(OpenSensePipe.ProtocolVersion, snapshot.ProtocolVersion);
        Assert.Equal("Nitro AN515-58 (simulated)", snapshot.DeviceName);
        Assert.Equal("NHQ7PEU00A1230ABCD7600", snapshot.SerialNumber);
        Assert.Equal([FanId.Cpu, FanId.Gpu], snapshot.Capabilities.Fans.Select(f => f.Id));
        Assert.Contains(SensorId.CpuTemperature, snapshot.Detected.Sensors);
        Assert.True(snapshot.Capabilities.HasOperatingModes);
        Assert.Equal(4, snapshot.Capabilities.Keyboard.Zones);
        Assert.NotNull(snapshot.Firmware);
        Assert.Equal(FanControlMode.Auto, snapshot.Settings.Profile.Mode);
    }

    [Fact]
    public async Task Profile_changes_reach_the_control_loop_and_come_back_as_telemetry()
    {
        var maxSeen = new TaskCompletionSource<Telemetry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.EffectiveMode == FanControlMode.Max)
                maxSeen.TrySetResult(t);
        };

        await _service.SetProfileAsync(new ControlProfile { Mode = FanControlMode.Max }, TestContext.Current.CancellationToken);
        var telemetry = await maxSeen.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.All(telemetry.Fans, f => Assert.Equal(FanBehavior.Max, f.Behavior));
        Assert.NotNull(telemetry.CpuTemperature);
        // The GPU's limit is its driver's; the CPU reports none here, so Intel's usual TjMax stands.
        Assert.Equal(new ThermalLimits(ThermalLimits.IntelCpuDefault, 83), telemetry.Limits);
        var snapshot = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(FanControlMode.Max, snapshot.Settings.Profile.Mode);
    }

    [Fact]
    public async Task Gpu_clock_offsets_by_mode_cross_the_wire_and_come_back_as_telemetry()
    {
        var applied = new TaskCompletionSource<Telemetry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.GpuClocks?.Applied is { CoreMhz: 150 })
                applied.TrySetResult(t);
        };
        var profile = new ControlProfile
        {
            OperatingMode = OperatingMode.Performance,
            GpuClocks = new GpuClockSettings { Modes = new Dictionary<OperatingMode, ClockOffsets> { [OperatingMode.Performance] = new(150, 500) } },
        };

        await _service.SetProfileAsync(profile, TestContext.Current.CancellationToken);
        var telemetry = await applied.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(new GpuClockTelemetry(FakeGpuClocks.AcerLimits, new ClockOffsets(150, 500), new ClockOffsets(150, 500)), telemetry.GpuClocks);
        var snapshot = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new ClockOffsets(150, 500), snapshot.Settings.Profile.GpuClocks.Modes[OperatingMode.Performance]);
        // The limits are remembered for next time, when the GPU may be asleep.
        Assert.Contains("610.62", await _service.GetDiagnosticsAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains("\"CoreMax\": 1000", await File.ReadAllTextAsync(Path.Combine(_directory, "state.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overrides_rebuild_the_session_and_notify_clients()
    {
        var rebuilt = new TaskCompletionSource<EngineSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Rebuilt += (_, s) => rebuilt.TrySetResult(s);

        await _service.SetOverridesAsync(new CapabilityOverrides { OperatingModes = false }, TestContext.Current.CancellationToken);
        var snapshot = await rebuilt.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.False(snapshot.Capabilities.HasOperatingModes);
        Assert.True(snapshot.Detected.HasOperatingModes);
        Assert.False(snapshot.Settings.Overrides.OperatingModes);
    }

    [Fact]
    public async Task Keyboard_settings_are_applied_and_read_back()
    {
        await _service.SetKeyboardAsync(new KeyboardSettings { WindowsKey = false }, TestContext.Current.CancellationToken);

        var state = await _service.ReadKeyboardAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(state);
        Assert.False(state.WindowsKey);
    }

    [Fact]
    public async Task Lighting_is_applied_and_read_back()
    {
        var snapshot = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var keyboard = Assert.Single(snapshot.Capabilities.Lights, l => l.Location == LightingLocation.Keyboard);
        Assert.Contains(keyboard.Effects, e => e.Effect == LightingEffect.Wave && e.Direction);
        Assert.NotNull(snapshot.Lighting?.GetValueOrDefault(keyboard.Id));

        var lighting = new LightingSettings { Effect = LightingEffect.Wave, Speed = 7, Brightness = 50, Direction = LightingDirection.Left };
        await _service.SetLightingAsync(new LightingConfig().With(keyboard.Id, lighting), TestContext.Current.CancellationToken);

        var state = await _service.ReadLightingAsync(TestContext.Current.CancellationToken);
        var shown = state?.GetValueOrDefault(keyboard.Id);
        Assert.NotNull(shown);
        Assert.Equal(LightingEffect.Wave, shown.Effect);
        Assert.Equal(7, shown.Speed);
        Assert.Equal(50, shown.Brightness);
        Assert.Equal(LightingDirection.Left, shown.Direction);
        var settings = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LightingEffect.Wave, settings.Settings.Lighting.For(keyboard.Id)?.Effect);
    }

    [Fact]
    public async Task Power_settings_and_calibration_cross_the_wire()
    {
        var battery = new TaskCompletionSource<Telemetry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.Battery?.CalibrationStarted is not null)
                battery.TrySetResult(t);
        };
        var before = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);

        await _service.SetPowerAsync(new PowerSettings { ChargeLimit = true, UsbCharging = new UsbChargingSettings(true, 20) },
            TestContext.Current.CancellationToken);
        var started = await _service.StartBatteryCalibrationAsync(TestContext.Current.CancellationToken);
        var telemetry = await battery.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await _service.StopBatteryCalibrationAsync(TestContext.Current.CancellationToken);
        var after = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.True(before.Capabilities.Battery.Calibration);
        Assert.True(before.Capabilities.UsbCharging);
        Assert.Equal(new UsbChargingState(false, null), before.Firmware!.UsbCharging);
        Assert.Equal(CalibrationResult.Started, started);
        Assert.Equal(80, telemetry.Battery!.Percent);
        Assert.Equal(new UsbChargingSettings(true, 20), after.Settings.Power.UsbCharging);
        Assert.True(after.Settings.Power.ChargeLimit);
    }

    [Fact]
    public async Task Diagnostics_cross_the_wire()
    {
        var diagnostics = await _service.GetDiagnosticsAsync(TestContext.Current.CancellationToken);

        Assert.Contains("capability probe", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Recent firmware events:", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_settings_and_the_boot_logo_cross_the_wire()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshot = await _service.GetSnapshotAsync(ct);
        Assert.True(snapshot.Capabilities.BootAnimation);
        Assert.True(snapshot.Capabilities.CustomBootLogo);
        Assert.True(await _service.SetBootAnimationAsync(false, ct));

        var gif = Pictures.Gif(300, 200);
        Assert.Equal(BootLogoResult.Done, await _service.SetBootLogoAsync(gif, ct));
        var state = await _service.GetBootLogoAsync(ct);
        Assert.True(state.Custom);
        Assert.Equal(BootLogoFormat.Gif, state.Format);
        Assert.Equal(gif, state.Image);
        Assert.Equal(new PixelSize(1920, 1080), state.Screen);

        Assert.Equal(BootLogoResult.TooManyPixels, await _service.SetBootLogoAsync(Pictures.Gif(1000, 200), ct));
        Assert.Equal(BootLogoResult.NotBaseline, await _service.SetBootLogoAsync(Pictures.Jpeg(300, 200, frame: 0xC2), ct));
        Assert.True(await _service.RestoreBootLogoAsync(ct));
    }

    [Fact]
    public async Task Battery_health_crosses_the_wire()
    {
        var health = await _service.ReadBatteryHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BatteryHealth(58751, 37853, 74, false) { Manufacturer = "SMP", Name = "AP18E7M" }, health);
        Assert.Equal(64, health!.Percent);
    }

    [Fact]
    public async Task The_fan_table_and_the_setup_logo_switch_cross_the_wire()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshot = await _service.GetSnapshotAsync(ct);
        Assert.True(snapshot.Capabilities.FanTable);
        Assert.True(snapshot.Capabilities.CustomBootLogoSwitch);
        Assert.Null(snapshot.Firmware!.FanTable); // none picked yet
        Assert.Null(snapshot.Settings.Profile.FanTable);

        await _service.SetProfileAsync(new ControlProfile { FanTable = FanTable.Fastest }, ct);

        Assert.True(SpinWait.SpinUntil(() => _machine.Laptop!.FanTable == (byte)FanTable.Fastest, Timeout));
        Assert.Equal(FanTable.Fastest, (await _service.GetSnapshotAsync(ct)).Settings.Profile.FanTable);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
        _engine.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
