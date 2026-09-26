using Microsoft.Extensions.Time.Testing;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

public class KeyboardProtocolTests
{
    [Fact]
    public void Static_payload_matches_acer_service_layout()
    {
        var payload = KeyboardProtocol.BacklightPayload(KeyboardEffect.Static, 7, 75, KeyboardDirection.Left, new RgbColor(1, 2, 3));
        // Static ignores speed, direction and colour; the record repeats with byte 9 forced to 1.
        Assert.Equal(new byte[] { 0, 0, 75, 0, 0, 0, 0, 0, 0, 1, 75, 0, 0, 0, 0, 0 }, payload);
    }

    [Fact]
    public void Wave_payload_carries_flag_and_direction_but_no_colour()
    {
        var payload = KeyboardProtocol.BacklightPayload(KeyboardEffect.Wave, 4, 100, KeyboardDirection.Left, new RgbColor(0x10, 0x20, 0x30));
        Assert.Equal(new byte[] { 3, 4, 100, 0x08, 2, 0, 0, 0, 3, 1, 100, 0x08, 2, 0, 0, 0 }, payload);
    }

    [Fact]
    public void Neon_has_no_colour_and_speed_is_clamped()
    {
        var payload = KeyboardProtocol.BacklightPayload(KeyboardEffect.Neon, 42, 50, KeyboardDirection.Right, new RgbColor(9, 9, 9));
        Assert.Equal(new byte[] { 2, 9, 50, 0, 0, 0, 0, 0 }, payload[..8]);
    }

    [Theory]
    [InlineData(KeyboardEffect.Meteor)]
    [InlineData(KeyboardEffect.Twinkling)]
    public void Meteor_and_twinkling_use_predatorsense_layout(KeyboardEffect effect)
    {
        // Speed and colour but no direction; bytes 8-15 are 03 01 and zeros, as PredatorSense sends them.
        var payload = KeyboardProtocol.BacklightPayload(effect, 5, 100, KeyboardDirection.Left, new RgbColor(0x00, 0xA0, 0xFF));
        Assert.Equal(new byte[] { (byte)effect, 5, 100, 0, 0, 0x00, 0xA0, 0xFF, 0x03, 0x01, 0, 0, 0, 0, 0, 0 }, payload);
    }

    [Fact]
    public void Zone_encodings()
    {
        Assert.Equal(0x30201004UL, KeyboardProtocol.ZoneColorInput(3, new RgbColor(0x10, 0x20, 0x30)));
        Assert.Equal(0x08UL | (1UL << 40) | (1UL << 43), KeyboardProtocol.ZoneEnableInput([true, false, false, true]));

        // PredatorSense's array forms: the integer's bytes, then zeros (not NitroSense's repeat, which lands on light bars).
        var input = KeyboardProtocol.ZoneEnableInput([true, false, false, true]);
        Assert.Equal(Convert.FromHexString("080000000009000000000000"), KeyboardProtocol.ZoneEnableArray(input, 12));
        Assert.Equal(Convert.FromHexString("08000000000900000000000000000000"), KeyboardProtocol.ZoneEnableArray(input, 16));
    }

    [Fact]
    public void Backlight_timeout_and_profile_encodings()
    {
        Assert.Equal(0x88401u, KeyboardProtocol.BacklightTimeoutQuery(0x84));
        Assert.Equal(0x1E_64_0000_0000UL | 0x88402UL, KeyboardProtocol.BacklightTimeoutInput(0x84, 100, 30));
        Assert.Equal(0x0100_0002UL, KeyboardProtocol.WindowsKeyInput(true));
        Assert.Equal(0x0001_0000_0000_0010UL, KeyboardProtocol.LcdOverdriveInput(true));

        const ulong an515Profile = 0xFFFF000103FF00; // real AN515-57 answer
        Assert.True(KeyboardProtocol.WindowsKeyValue(an515Profile));
        Assert.False(KeyboardProtocol.LcdOverdriveValue(an515Profile));
        Assert.False(KeyboardProtocol.LcdOverdriveSupported(an515Profile)); // 0xFF: its panel has none
        Assert.True(KeyboardProtocol.LcdOverdriveSupported(0x0001_0000_0103_FF00));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(90, 100)]
    [InlineData(62, 50)]
    [InlineData(63, 75)]
    [InlineData(10, 0)]
    public void Backlight_timeout_brightness_goes_out_as_a_level_the_firmware_keeps(int percent, int level)
    {
        Assert.Equal((ulong)level, (KeyboardProtocol.BacklightTimeoutInput(0x84, percent, 30) >> 32) & 0xFF);
    }

    [Fact]
    public void Zone_read_back_encodings()
    {
        Assert.Equal(0x1u, KeyboardProtocol.ZoneColorQuery(1));
        Assert.Equal(0x8u, KeyboardProtocol.ZoneColorQuery(4));
        Assert.Equal(new RgbColor(0xFF, 0x2D, 0x55), KeyboardProtocol.ZoneColorValue(0x552DFF00)); // AN515-57
        Assert.Equal([true, true, true, true], KeyboardProtocol.ZoneEnableValue(0xF00_0000_0000, 4)); // AN515-57
        Assert.Equal([true, false, false, true], KeyboardProtocol.ZoneEnableValue(KeyboardProtocol.ZoneEnableInput([true, false, false, true]), 4));
        Assert.Equal([true, false], KeyboardProtocol.ZoneEnableValue(0x100_0000_0000, 2));
    }

    [Fact]
    public void Overdrive_needs_the_firmwares_answer_even_where_nitrosense_lists_it()
    {
        var hints = new NitroSenseHints { KeyboardColor = 2, KeyboardZones = 4, AdvancedSettings = ["LCD"] };
        var none = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { Profile = 0xFFFF000103FF00 }), hints);
        var some = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { Profile = 0x0000_0001_0103_FF00 }), hints);

        Assert.False(none.Keyboard.LcdOverdrive);
        Assert.True(some.Keyboard.LcdOverdrive);
    }
}

public class SmbiosTests
{
    // Acer structures captured from an AN515-57 (BIOS V1.17), plus the end-of-table marker.
    private static readonly byte[] An515Tables = Convert.FromHexString(
        "AA561A0001080000EF000F000E1003020108410204004202200043024000440208004502100048020100490202006102080062020100630202006402040081020400830202008402080087020002880200048A020010" + "0000" +
        "AC391C00025301FF0002030003FF0004FF00050F0006FF0007FF00080400090200" + "0A02000B02000CFF000D00000E01000F010010FF0011FF00" + "0000" +
        "7F040000" + "0000");

    [Fact]
    public void Parses_gaming_interface_version_and_hotkeys()
    {
        var smbios = AcerSmbios.Parse(An515Tables);

        Assert.Equal((byte)2, smbios.GamingMajor);
        Assert.Equal((byte)0x53, smbios.GamingMinor);
        Assert.Equal(2.83, smbios.GamingVersion!.Value, 2);
        Assert.Equal(8, smbios.LedArrayLength);
        Assert.False(smbios.HasEcLightBars);
        Assert.Equal(17, smbios.GamingRecords.Count);
        Assert.Contains(new AcerSmbiosRecord(0x05, 0x0F), smbios.GamingRecords);
        Assert.True(smbios.HasHotkeyFunction(0x84));
    }

    [Theory]
    [InlineData(2, 0x55, 8)]
    [InlineData(2, 0x56, 12)]
    [InlineData(2, 0x5A, 12)]
    [InlineData(2, 0x5B, 16)]
    [InlineData(3, 0x00, 16)]
    [InlineData(1, 0x63, 8)]
    public void Interface_version_picks_the_led_array_length(byte major, byte minor, int length) =>
        Assert.Equal(length, new AcerSmbios(major, minor, [], []).LedArrayLength);

    [Fact]
    public void Record_0x17_of_1_means_light_bars_on_the_embedded_controller()
    {
        Assert.True(new AcerSmbios(2, 0x5B, [new(0x17, 1)], []).HasEcLightBars);
        Assert.False(new AcerSmbios(2, 0x5B, [new(0x17, 2)], []).HasEcLightBars); // USB light bars
    }
}

public class KeyboardServiceTests
{
    private static (KeyboardService Service, SimulatedTransport Laptop) Create(TimeProvider? time = null)
    {
        var laptop = new SimulatedTransport(SimulatedModel.Nitro2021);
        var device = new AcerDevice(laptop);
        var caps = new KeyboardCapabilities { RgbBacklight = true, Zones = 4, BacklightHotkey = 0x84, WindowsKey = true, LcdOverdrive = true };
        return (new KeyboardService(new ImmediateDispatcher(device), caps, time: time), laptop);
    }

    [Fact]
    public async Task After_a_sleep_the_settings_go_out_again_once_Acers_agent_has_restored_its_own()
    {
        var time = new FakeTimeProvider();
        var (service, laptop) = Create(time);
        await service.ApplyAsync(new KeyboardSettings { WindowsKey = false, LcdOverdrive = true });

        var resumed = service.OnResume();
        var acersAgent = new AcerDevice(laptop);
        acersAgent.SetWindowsKeyEnabled(true);
        acersAgent.SetLcdOverdrive(false);
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(resumed.IsCompleted); // not while the agent may still overwrite them

        time.Advance(TimeSpan.FromSeconds(1));
        await resumed;
        var state = await service.ReadStateAsync();
        Assert.False(state.WindowsKey);
        Assert.True(state.LcdOverdrive);
    }

    [Fact]
    public async Task Settings_round_trip_through_firmware_state()
    {
        var (service, _) = Create();

        await service.ApplyAsync(new KeyboardSettings { WindowsKey = false, LcdOverdrive = true, BacklightAutoOff = false });
        var state = await service.ReadStateAsync();

        Assert.False(state.WindowsKey);
        Assert.True(state.LcdOverdrive);
        Assert.False(state.BacklightAutoOff);
    }

    [Fact]
    public async Task Null_settings_leave_firmware_untouched()
    {
        var (service, laptop) = Create();
        var before = await service.ReadStateAsync();

        await service.ApplyAsync(new KeyboardSettings());

        Assert.Equal(before, await service.ReadStateAsync());
        Assert.Equal((byte)KeyboardEffect.Static, laptop.Backlight[0]);
    }
}
