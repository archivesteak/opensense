using System.Management;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Tests;

public class WmiValueTests
{
    [Fact]
    public void Integers_take_the_declared_parameter_type()
    {
        Assert.Equal((byte)1, WmiValues.ToCim(CimType.UInt8, false, 1));
        Assert.Equal((ushort)0x1234, WmiValues.ToCim(CimType.UInt16, false, 0x1234));
        Assert.Equal(0x04u, WmiValues.ToCim(CimType.UInt32, false, (byte)4));
        Assert.Equal(0x1E0F04UL, WmiValues.ToCim(CimType.UInt64, false, 0x1E0F04u));
    }

    [Fact]
    public void Byte_arrays_are_copied()
    {
        byte[] reserved = [0, 0, 0, 0, 0];
        var passed = Assert.IsType<byte[]>(WmiValues.ToCim(CimType.UInt8, true, reserved));
        Assert.Equal(reserved, passed);
        Assert.NotSame(reserved, passed);
    }

    [Fact]
    public void Values_that_do_not_fit_are_refused()
    {
        Assert.Throws<AcerWmiException>(() => WmiValues.ToCim(CimType.UInt8, false, 256));
        Assert.Throws<AcerWmiException>(() => WmiValues.ToCim(CimType.UInt8, true, 1));
        Assert.Throws<AcerWmiException>(() => WmiValues.ToCim(CimType.String, false, "x"));
    }

    [Fact]
    public void Outputs_read_as_integers_and_byte_arrays()
    {
        Assert.Equal(3UL, WmiValues.FromCim((byte)3));
        Assert.Equal(0x6400UL, WmiValues.FromCim(0x6400u));
        Assert.Equal([1, 0], Assert.IsType<byte[]>(WmiValues.FromCim(new byte[] { 1, 0 })));
        Assert.Null(WmiValues.FromCim("text"));
        Assert.Null(WmiValues.FromCim(null));

        var outputs = new WmiOutputs([KeyValuePair.Create<string, object>("uFunctionList", 3UL),
            KeyValuePair.Create<string, object>("uReturn", new byte[] { 0, 0 })]);
        Assert.Equal(3UL, outputs.Value("ufunctionlist"));
        Assert.Equal([0, 0], outputs.Bytes("uReturn"));
        Assert.Null(outputs.Value("uReturn"));
        Assert.Null(outputs.Bytes("missing"));
    }
}

public class FirmwareEventTests
{
    [Fact]
    public void Event_detail_decodes_kind_and_value()
    {
        // Unplugging and plugging in the adapter on the AN515-57.
        var unplugged = FirmwareEvent.Decode([0x08, 0x00, 0, 0, 0, 0, 0, 0])!;
        var plugged = FirmwareEvent.Decode([0x08, 0x01, 0, 0, 0, 0, 0, 0])!;

        Assert.Equal(FirmwareEventKind.AcAdapter, unplugged.Kind);
        Assert.Equal(0, unplugged.Value);
        Assert.Equal(1, plugged.Value);
        Assert.Equal(FirmwareEventKind.ModeKey, FirmwareEvent.Decode([0x07, 0x04])!.Kind);
        Assert.Null(FirmwareEvent.Decode([]));
        Assert.Null(FirmwareEvent.Decode(null));
    }

    [Fact]
    public async Task Diagnostics_list_recent_events_and_hid_devices()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
        var machine = new SimulatedMachine(SimulatedModel.Nitro2022);
        // A USB keyboard's lighting interface: listed, not probed (the embedded controller's has tests of its own).
        machine.Hid.Devices.Add(new FakeHidDevice(new HidDeviceInfo(@"\\?\hid#test", 0x04F2, 0x1516, 1, 0xFF02, 1, 0, 0, 9)));
        using (var engine = new OpenSenseEngine(machine, Path.Combine(directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance))
        {
            engine.Start();
            Assert.True(machine.Events.Started);

            machine.Events.Raise(0x08, 0x00, 0, 0, 0, 0, 0, 0);
            var diagnostics = await engine.GetDiagnosticsAsync(TestContext.Current.CancellationToken);

            Assert.Contains("AcAdapter 0800000000000000", diagnostics, StringComparison.Ordinal);
            Assert.Contains("04F2:1516 rev 0001 usage page FF02 usage 0001", diagnostics, StringComparison.Ordinal);
        }
        Assert.False(machine.Events.Started);
        Directory.Delete(directory, recursive: true);
    }
}
