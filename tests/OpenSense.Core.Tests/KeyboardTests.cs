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
        var payload = KeyboardProtocol.BacklightPayload(KeyboardEffect.Wave, 4, 100, KeyboardDirection.Up, new RgbColor(0x10, 0x20, 0x30));
        Assert.Equal(new byte[] { 3, 4, 100, 0x08, 3, 0, 0, 0, 3, 1, 100, 0x08, 3, 0, 0, 0 }, payload);
    }

    [Fact]
    public void Neon_has_no_colour_and_speed_is_clamped()
    {
        var payload = KeyboardProtocol.BacklightPayload(KeyboardEffect.Neon, 42, 50, KeyboardDirection.Right, new RgbColor(9, 9, 9));
        Assert.Equal(new byte[] { 2, 9, 50, 0, 0, 0, 0, 0 }, payload[..8]);
    }

    [Fact]
    public void Zone_encodings()
    {
        Assert.Equal(0x30201004UL, KeyboardProtocol.ZoneColorInput(3, new RgbColor(0x10, 0x20, 0x30)));
        Assert.Equal(0x08UL | (1UL << 40) | (1UL << 43), KeyboardProtocol.ZoneEnableInput([true, false, false, true]));

        var array = KeyboardProtocol.ZoneEnableArray(0x0000_0900_0000_0008UL);
        Assert.Equal(12, array.Length);
        Assert.Equal(array[..4], array[8..12]);
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
        Assert.False(smbios.UsesArrayLedBehavior);
        Assert.Equal(17, smbios.GamingRecords.Count);
        Assert.Contains(new AcerSmbiosRecord(0x05, 0x0F), smbios.GamingRecords);
        Assert.True(smbios.HasHotkeyFunction(0x84));
    }

    [Fact]
    public void Version_286_switches_to_array_zone_command() =>
        Assert.True(new AcerSmbios(2, 0x56, [], []).UsesArrayLedBehavior);
}

public class KeyboardServiceTests
{
    private sealed class ImmediateDispatcher(AcerDevice device) : IDeviceDispatcher
    {
        public Task<T> InvokeAsync<T>(Func<AcerDevice, T> action) => Task.FromResult(action(device));
    }

    private static (KeyboardService Service, SimulatedTransport Laptop) Create()
    {
        var laptop = new SimulatedTransport(SimulatedModel.Nitro2021);
        var device = new AcerDevice(laptop);
        var caps = new KeyboardCapabilities { RgbBacklight = true, Zones = 4, BacklightHotkey = 0x84, WindowsKey = true, LcdOverdrive = true };
        return (new KeyboardService(new ImmediateDispatcher(device), caps), laptop);
    }

    [Fact]
    public async Task Static_lighting_sets_zones_brightness_and_colours()
    {
        var (service, laptop) = Create();

        await service.ApplyAsync(new KeyboardSettings
        {
            Lighting = new LightingSettings
            {
                Brightness = 50,
                Zones = [new(true, "#102030"), new(false, "#FFFFFF"), new(true, "#00FF00"), new(true, "#0000FF")],
            },
        });

        Assert.Equal(new RgbColor(0x10, 0x20, 0x30), laptop.ZoneColor(1));
        Assert.Null(laptop.ZoneColor(2));
        Assert.Equal(new RgbColor(0, 255, 0), laptop.ZoneColor(3));
        Assert.Equal((byte)KeyboardEffect.Static, laptop.Backlight[0]);
        Assert.Equal(50, laptop.Backlight[2]);
    }

    [Fact]
    public async Task Effect_round_trips_through_firmware_state()
    {
        var (service, _) = Create();

        await service.ApplyAsync(new KeyboardSettings
        {
            Lighting = new LightingSettings { Effect = KeyboardEffect.Shifting, Speed = 3, Brightness = 75, Direction = KeyboardDirection.Left, EffectColor = "#FF8000" },
            WindowsKey = false,
            LcdOverdrive = true,
            BacklightAutoOff = false,
        });
        var state = await service.ReadStateAsync();

        Assert.Equal(KeyboardEffect.Shifting, state.Effect);
        Assert.Equal(3, state.Speed);
        Assert.Equal(75, state.Brightness);
        Assert.Equal(KeyboardDirection.Left, state.Direction);
        Assert.Equal(new RgbColor(0xFF, 0x80, 0x00), state.EffectColor);
        Assert.False(state.WindowsKey);
        Assert.True(state.LcdOverdrive);
        Assert.False(state.BacklightAutoOff);
    }

    [Fact]
    public async Task Null_settings_leave_firmware_untouched()
    {
        var (service, laptop) = Create();
        var before = laptop.Backlight;

        await service.ApplyAsync(new KeyboardSettings());

        Assert.Equal(before, laptop.Backlight);
    }
}
