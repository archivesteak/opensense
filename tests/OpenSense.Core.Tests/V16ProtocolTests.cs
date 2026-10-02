using OpenSense.Core.Hardware;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Tests;

public class V16ProtocolTests
{
    [Theory]
    [InlineData("Nitro ANV16-41")]
    [InlineData("Nitro ANV16-42")]
    [InlineData("Nitro ANV16-71")]
    [InlineData("Nitro ANV16S-71")]
    [InlineData("Nitro ANV16-A31")]
    [InlineData("Nitro ANV16S-41")]
    public void Absolute_profiles_write_only_the_requested_mode(string model)
    {
        var firmware = new FakeFirmware { SupportsModes = true };
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For(model) };
        Assert.True(device.SetOperatingMode(OperatingMode.Performance));
        var write = Assert.Single(firmware.Writes);
        Assert.Equal(AcerProtocol.MiscSetInput(MiscSetting.OperatingMode, (byte)OperatingMode.Performance), write.Input);
    }

    [Fact]
    public void A_failed_relative_reset_does_not_apply_a_relative_target()
    {
        var firmware = new FakeFirmware { SupportsModes = true, RejectEverything = true };
        Assert.False(new AcerDevice(firmware).SetOperatingMode(OperatingMode.Turbo));
        Assert.Equal(AcerProtocol.MiscSetInput(MiscSetting.OperatingMode, (byte)OperatingMode.Balanced), Assert.Single(firmware.Writes).Input);
    }

    [Fact]
    public void Unsupported_Wmi_modes_do_not_write_but_Hid_synchronization_writes_once()
    {
        var firmware = new FakeFirmware { SupportsModes = true };
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For("ANV16-I31", "V1.08") };
        Assert.Null(device.GetOperatingMode());
        Assert.False(device.SetOperatingMode(OperatingMode.Turbo));
        Assert.Empty(firmware.Calls);
        device.SynchronizeHidOperatingMode(OperatingMode.Turbo);
        Assert.Single(firmware.Writes);
    }

    [Fact]
    public void Broken_S61_boost_readback_is_not_adopted_but_valid_writes_remain()
    {
        var firmware = new FakeFirmware();
        firmware.Speed[1] = 70;
        firmware.Speed[4] = 30;
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For("Nitro ANV16S-61", "V1.14") };
        Assert.Null(device.GetFanBoost(FanChannel.Cpu));
        Assert.Null(device.GetFanBoost(FanChannel.Gpu));
        Assert.Empty(firmware.Calls);
        Assert.True(device.SetFanSpeed(FanChannel.Gpu, 40));
        Assert.Equal(AcerProtocol.FanSpeedInput(FanChannel.Gpu, 40), Assert.Single(firmware.Writes).Input);
    }

    [Fact]
    public void Missing_custom_boost_does_not_turn_a_default_percentage_into_adopted_state()
    {
        var state = new FirmwareState(new Dictionary<FanId, FanBehavior?> { [FanId.Cpu] = FanBehavior.Custom },
            new Dictionary<FanId, int?> { [FanId.Cpu] = null }, null, null, null);
        var configured = new ControlProfile { Mode = FanControlMode.Auto };
        Assert.Equal(FanControlMode.Auto, state.ToProfile(configured).Mode);
    }

    [Fact]
    public void Initial_mode_comes_from_the_active_Hid_interface()
    {
        var bus = new FakeHidBus();
        bus.Devices.Add(new FakeEcHid { Mode = 0 });
        using var hid = EcHidDevice.Open(bus, machineLock: false)!;
        var firmware = new FakeFirmware { SupportsModes = true };
        var device = new AcerDevice(firmware) { EcHid = hid, FirmwareProfile = AcerFirmwareProfile.For("ANV16-I31", "1.08") };
        var modes = EcHidProtocol.Modes(5);
        var capabilities = DeviceCapabilities.None with { OperatingModes = modes, EcHid = new EcHidCapabilities(new EcHidVersion(0, 7)) { Modes = modes } };
        Assert.Equal(OperatingMode.Turbo, FirmwareState.Read(device, capabilities).OperatingMode);
        Assert.DoesNotContain(firmware.Calls, c => c.Method == "GetGamingMiscSetting");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Zone_arrays_decode_the_mask_after_accounting_for_the_status_field(bool separateStatus)
    {
        var bytes = new byte[16];
        bytes[separateStatus ? 4 : 5] = 0b1001;
        var outputs = separateStatus
            ? FakeFirmware.Outputs(("gmReturn", 0UL), ("gmOutput", bytes))
            : FakeFirmware.Outputs(("gmOutput", bytes));
        var device = new AcerDevice(new OutputsTransport(outputs));
        Assert.Equal([true, false, false, true], device.GetZonesEnabled(4));
    }

    [Fact]
    public void Failed_or_short_zone_reads_remain_unknown()
    {
        var failed = new AcerDevice(new OutputsTransport(FakeFirmware.Outputs(("gmReturn", 1UL), ("gmOutput", new byte[16]))));
        var shortRecord = new AcerDevice(new OutputsTransport(FakeFirmware.Outputs(("gmReturn", 0UL), ("gmOutput", new byte[4]))));
        Assert.Null(failed.GetZonesEnabled(4));
        Assert.Null(shortRecord.GetZonesEnabled(4));
    }

    [Fact]
    public void Ac_source_four_is_preserved_without_turning_it_into_adapter_one()
    {
        var change = FirmwareEvent.Decode([8, 4, 0]);
        Assert.Equal((byte)4, change!.Value);
        Assert.Equal("AcAdapter 080400", change.ToString());
    }

    private sealed class OutputsTransport(WmiOutputs outputs) : IWmiTransport
    {
        public bool IsClassAvailable(string className) => true;
        public ulong Invoke(string className, string method, ulong input) => throw new NotSupportedException();
        public WmiArrayResult InvokeArray(string className, string method, byte[]? input) => throw new NotSupportedException();
        public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs) => outputs;
        public IReadOnlyList<string> ReadStrings(string className, string propertyName) => [];
        public bool HasArrayInput(string className, string method) => false;
        public void Dispose() { }
    }
}

public class WmiSchemaTests
{
    [Fact]
    public void Named_payload_and_array_status_are_chosen_independently_of_property_order()
    {
        KeyValuePair<string, object?>[] outputs = [new("Unrelated", 999UL), new("gmReturn", 2UL), new("gmOutput", 0x123400UL)];
        Assert.Equal(0x123400UL, WmiValues.Scalar(outputs));
        Assert.Equal(2UL, WmiValues.Scalar(outputs, statusOnly: true));
    }

    [Fact]
    public void Malformed_or_ambiguous_outputs_become_firmware_errors()
    {
        Assert.Throws<AcerWmiException>(() => WmiValues.Scalar([new("gmOutput", "not an integer")]));
        Assert.Throws<AcerWmiException>(() => WmiValues.Scalar([new("One", 1UL), new("Two", 2UL)]));
        Assert.Null(WmiValues.Scalar([new("ReturnValue", 0UL)]));
    }
}
