using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

public sealed class FnLockTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(0, null)]
    [InlineData(255, null)]
    public void Only_defined_misc_values_are_states(byte value, bool? expected)
    {
        using var firmware = new FnLockFirmware { Value = value };
        Assert.Equal(expected, new AcerDevice(firmware).GetFnLock());
        firmware.GetterStatus = 1;
        Assert.Null(new AcerDevice(firmware).GetFnLock());
    }

    [Theory]
    [InlineData("Nitro ANV16-71", "V1.11", 1, true, true)]
    [InlineData("Nitro ANV16S-71", "V1.06", 2, true, true)]
    [InlineData("Nitro ANV16-71", "V1.11", 0, false, true)]
    [InlineData("Nitro ANV16S-71", "V1.06", 255, false, true)]
    [InlineData("Nitro ANV16-42", "V1.15", 1, false, false)]
    [InlineData("Unknown laptop", "V1.00", 1, false, false)]
    public void The_control_is_exposed_only_for_a_profile_with_a_valid_runtime_state(string model, string biosVersion,
        byte value, bool expected, bool queried)
    {
        using var firmware = new FnLockFirmware { Value = value };
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For(model, biosVersion) };
        var caps = CapabilityProbe.Probe(device, model: model);

        Assert.Equal(expected, caps.Keyboard.FnLock);
        Assert.Equal(queried, firmware.Reads > 0);
        Assert.Empty(firmware.Writes);
    }

    [Fact]
    public void Setters_use_the_two_defined_values_and_keep_the_firmware_status()
    {
        using var firmware = new FnLockFirmware { Value = 2 };
        var device = new AcerDevice(firmware);
        Assert.True(device.SetFnLock(true));
        Assert.True(device.GetFnLock());
        Assert.True(device.SetFnLock(false));
        Assert.False(device.GetFnLock());
        firmware.RejectWrites = true;
        Assert.False(device.SetFnLock(true));
        Assert.False(device.GetFnLock());
        Assert.Equal([0x010FUL, 0x020FUL, 0x010FUL], firmware.Writes);
    }

    [Fact]
    public async Task Configured_changes_are_serialized_deduplicated_and_restored_after_resume()
    {
        using var firmware = new FnLockFirmware();
        var device = new AcerDevice(firmware);
        var time = new FakeTimeProvider();
        var service = new KeyboardService(new ImmediateDispatcher(device), new KeyboardCapabilities { FnLock = true }, time: time);
        var settings = new KeyboardSettings { FnLock = false };
        await service.ApplyAsync(settings);
        Assert.False((await service.ReadStateAsync()).FnLock);
        Assert.Equal([0x020FUL], firmware.Writes);

        await service.ApplyAsync(settings);
        Assert.Single(firmware.Writes);
        var resumed = service.OnResume();
        firmware.Value = 1; // Acer's agent restores its own state first.
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(resumed.IsCompleted);
        time.Advance(TimeSpan.FromSeconds(1));
        await resumed;
        Assert.False((await service.ReadStateAsync()).FnLock);
        Assert.Equal([0x020FUL, 0x020FUL], firmware.Writes);
        await service.StopAsync();
    }

    [Fact]
    public async Task Unsupported_or_unconfigured_controls_make_no_writes_and_rejections_can_be_retried()
    {
        using var firmware = new FnLockFirmware { RejectWrites = true };
        var device = new AcerDevice(firmware);
        var unsupported = new KeyboardService(new ImmediateDispatcher(device), new KeyboardCapabilities());
        await unsupported.ApplyAsync(new KeyboardSettings { FnLock = false });
        Assert.Empty(firmware.Writes);
        Assert.Null((await unsupported.ReadStateAsync()).FnLock);
        await unsupported.StopAsync();

        var service = new KeyboardService(new ImmediateDispatcher(device), new KeyboardCapabilities { FnLock = true });
        var notices = new List<ControlNotice>();
        service.Notice += notices.Add;
        await service.ApplyAsync(new KeyboardSettings());
        Assert.Empty(firmware.Writes);
        await service.ApplyAsync(new KeyboardSettings { FnLock = false });
        Assert.Equal(NoticeKind.FnLockRejected, Assert.Single(notices).Kind);
        Assert.True((await service.ReadStateAsync()).FnLock);
        firmware.RejectWrites = false;
        await service.ApplyAsync(new KeyboardSettings { FnLock = false });
        Assert.False((await service.ReadStateAsync()).FnLock);
        Assert.Equal([0x020FUL, 0x020FUL], firmware.Writes);
        await service.StopAsync();
    }

    [Fact]
    public void The_additive_setting_and_state_round_trip_without_claiming_a_value_when_absent()
    {
        Assert.Null(JsonSerializer.Deserialize<KeyboardSettings>("{}")!.FnLock);
        var settings = new KeyboardSettings { FnLock = false };
        Assert.Equal(settings, JsonSerializer.Deserialize<KeyboardSettings>(JsonSerializer.Serialize(settings)));
        var state = new KeyboardState(null, null, null) { FnLock = true };
        Assert.Equal(state, JsonSerializer.Deserialize<KeyboardState>(JsonSerializer.Serialize(state)));
    }
}

file sealed class FnLockFirmware : IWmiTransport
{
    private readonly FakeFirmware _other = new();

    public byte Value { get; set; } = 1;
    public byte GetterStatus { get; set; }
    public bool RejectWrites { get; set; }
    public int Reads { get; private set; }
    public List<ulong> Writes { get; } = [];

    public ulong Invoke(string className, string method, ulong input)
    {
        if (className == AcerProtocol.GamingClass && (byte)input == (byte)MiscSetting.FnLock)
        {
            if (method == "GetGamingMiscSetting")
            {
                Reads++;
                return ((ulong)Value << 8) | GetterStatus;
            }
            if (method == "SetGamingMiscSetting")
            {
                Writes.Add(input);
                if (RejectWrites)
                    return 1;
                Value = (byte)(input >> 8);
                return 0;
            }
        }
        return _other.Invoke(className, method, input);
    }

    public bool IsClassAvailable(string className) => _other.IsClassAvailable(className);
    public WmiArrayResult InvokeArray(string className, string method, byte[]? input) => _other.InvokeArray(className, method, input);
    public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs) => _other.InvokeNamed(className, method, inputs);
    public IReadOnlyList<string> ReadStrings(string className, string propertyName) => _other.ReadStrings(className, propertyName);
    public bool HasArrayInput(string className, string method) => _other.HasArrayInput(className, method);
    public void Dispose() => _other.Dispose();
}
