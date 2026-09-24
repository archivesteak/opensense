using Microsoft.Extensions.Logging.Abstractions;
using Nerdbank.Streams;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;

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

    public ValueTask InitializeAsync()
    {
        _engine = new OpenSenseEngine(new SimulatedMachine(SimulatedModel.Nitro2022), Path.Combine(_directory, "settings.json"),
            NullLogger<OpenSenseEngine>.Instance);
        _engine.Start();

        var (serverStream, clientStream) = FullDuplexStream.CreatePair();
        _server = OpenSensePipe.CreateRpc(serverStream);
        _server.AddLocalRpcTarget<IOpenSenseService>(_engine, new StreamJsonRpc.JsonRpcTargetOptions { NotifyClientOfEvents = true });
        _server.StartListening();

        _client = OpenSensePipe.CreateRpc(clientStream);
        _service = _client.Attach<IOpenSenseService>();
        _client.StartListening();
        return ValueTask.CompletedTask;
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
        var snapshot = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(FanControlMode.Max, snapshot.Settings.Profile.Mode);
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
        var lighting = new LightingSettings { Effect = KeyboardEffect.Wave, Speed = 7, Brightness = 50 };
        await _service.SetKeyboardAsync(new KeyboardSettings { Lighting = lighting, WindowsKey = false }, TestContext.Current.CancellationToken);

        var state = await _service.ReadKeyboardAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(state);
        Assert.Equal(KeyboardEffect.Wave, state.Effect);
        Assert.Equal(7, state.Speed);
        Assert.False(state.WindowsKey);
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
