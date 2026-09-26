using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;

namespace OpenSense.Core.Tests;

/// <summary>Headers just long enough for <see cref="BootLogoImage.Read"/>.</summary>
internal static class Pictures
{
    public static byte[] Gif(int width, int height, string version = "GIF89a")
    {
        var data = new byte[32];
        System.Text.Encoding.ASCII.GetBytes(version).CopyTo(data, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), (ushort)height);
        data[10] = 0xF7; // global colour table of 256 entries
        return data;
    }

    /// <summary>
    /// SOI, a JFIF APP0 segment, a quantization table, a frame header of kind <paramref name="frame"/>, a Huffman table,
    /// a scan of all components with a few bytes of data (a stuffed FF and a restart among them), and EOI.
    /// </summary>
    /// <param name="table">The quantization table's precision and number.</param>
    /// <param name="scanIds">Adds this to the components' ids in the scan (1, 2, 3).</param>
    /// <param name="after">Bytes after EOI.</param>
    public static byte[] Jpeg(int width, int height, byte components = 3, byte frame = 0xC0, byte precision = 8,
        byte sampling = 0x11, byte table = 0x00, int scanIds = 0, byte[]? after = null)
    {
        var data = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        data.AddRange("JFIF\0"u8.ToArray());
        data.AddRange(new byte[9]);
        data.AddRange([0xFF, 0xDB, 0x00, 67, table]);
        data.AddRange(Enumerable.Repeat((byte)1, 64));
        var length = 8 + 3 * components;
        data.AddRange([0xFF, frame, (byte)(length >> 8), (byte)length, precision,
            (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, components]);
        for (var c = 1; c <= components; c++)
            data.AddRange([(byte)c, sampling, 0x00]);
        data.AddRange([0xFF, 0xC4, 0x00, 20, 0x00, 1]); // DC table 0: one code of one bit
        data.AddRange(new byte[15]);
        data.Add(0);
        length = 6 + 2 * components;
        data.AddRange([0xFF, 0xDA, (byte)(length >> 8), (byte)length, components]);
        for (var c = 1; c <= components; c++)
            data.AddRange([(byte)(c + scanIds), 0x00]);
        data.AddRange([0, 63, 0]);
        data.AddRange([0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD0, 0x56, 0xFF, 0xD9]);
        data.AddRange(after ?? []);
        return [.. data];
    }
}

public class BootLogoImageTests
{
    [Theory]
    [InlineData("GIF87a")]
    [InlineData("GIF89a")]
    public void Reads_a_gif_header(string version)
    {
        Assert.Equal(new BootLogoImage(BootLogoFormat.Gif, 640, 360, 8), BootLogoImage.Read(Pictures.Gif(640, 360, version)));
    }

    [Theory]
    [InlineData(0xC0, true)] // baseline
    [InlineData(0xC2, false)] // progressive
    public void Reads_a_jpeg_frame_header_after_other_segments(byte frame, bool baseline)
    {
        Assert.Equal(new BootLogoImage(BootLogoFormat.Jpeg, 700, 400, 24) { Baseline = baseline },
            BootLogoImage.Read(Pictures.Jpeg(700, 400, frame: frame)));
    }

    public static TheoryData<string, byte[]> JpegsTheFirmwareCantDecode => new()
    {
        { "extended", Pictures.Jpeg(100, 100, frame: 0xC1) },
        { "arithmetic", Pictures.Jpeg(100, 100, frame: 0xC9) },
        { "12 bits", Pictures.Jpeg(100, 100, components: 1, precision: 12) },
        { "sampled 3 times", Pictures.Jpeg(100, 100, sampling: 0x31) },
        { "sampled 4 times in each of three", Pictures.Jpeg(100, 100, sampling: 0x22) },
        { "16-bit quantization table", Pictures.Jpeg(100, 100, table: 0x10) },
        { "ids above 3, like Adobe's letters", Pictures.Jpeg(100, 100, scanIds: 'R' - 1) },
        { "a picture after the end", Pictures.Jpeg(100, 100, after: Pictures.Jpeg(160, 120)) },
    };

    [Theory]
    [MemberData(nameof(JpegsTheFirmwareCantDecode))]
    public void Jpegs_the_firmware_cant_decode(string kind, byte[] jpeg)
    {
        Assert.False(BootLogoImage.Read(jpeg)!.Baseline, kind);
        Assert.Equal(BootLogoProblem.NotBaseline, BootLogoRules.Check(jpeg, new PixelSize(1920, 1080), out _));
    }

    [Fact]
    public void Two_samples_per_block_and_restarts_are_fine()
    {
        Assert.True(BootLogoImage.Read(Pictures.Jpeg(100, 100, sampling: 0x21))!.Baseline);
        Assert.True(BootLogoImage.Read(Pictures.Jpeg(100, 100, components: 1))!.Baseline);
        // Zeros after the end don't matter: the firmware looks only for markers.
        Assert.True(BootLogoImage.Read(Pictures.Jpeg(100, 100, after: new byte[16]))!.Baseline);
    }

    [Fact]
    public void A_scan_of_one_component_of_three_is_not_decoded()
    {
        var jpeg = Pictures.Jpeg(100, 100).ToList();
        var scan = jpeg.FindLastIndex(b => b == 0xDA) - 1;
        jpeg.RemoveRange(scan, 4 + 1 + 2 * 3 + 3);
        jpeg.InsertRange(scan, [0xFF, 0xDA, 0x00, 8, 1, 1, 0x00, 0, 63, 0]);
        Assert.False(BootLogoImage.Read([.. jpeg])!.Baseline);
    }

    [Fact]
    public void Cmyk_jpeg_is_32_bits_per_pixel()
    {
        Assert.Equal(32, BootLogoImage.Read(Pictures.Jpeg(100, 100, components: 4))!.BitsPerPixel);
    }

    [Fact]
    public void Huffman_table_segment_is_not_taken_for_a_frame_header()
    {
        var jpeg = Pictures.Jpeg(100, 50).ToList();
        jpeg.InsertRange(2, [0xFF, 0xC4, 0x00, 0x04, 0x00, 0x00]); // DHT before the frame header
        Assert.Equal(100, BootLogoImage.Read([.. jpeg])!.Width);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0 })] // PNG
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02 })] // scan before any frame header
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x40, 0x00 })] // segment longer than the file
    [InlineData(new byte[] { 0x47, 0x49, 0x46 })] // "GIF", cut short
    public void Other_or_broken_files_are_not_read(byte[] data)
    {
        Assert.Null(BootLogoImage.Read(data));
    }

    [Fact]
    public void Acer_rules()
    {
        var screen = new PixelSize(1920, 1080);
        Assert.Equal(new PixelSize(768, 432), BootLogoRules.MaxSize(screen));

        Assert.Equal(BootLogoProblem.None, BootLogoRules.Check(Pictures.Jpeg(768, 432), screen, out _));
        Assert.Equal(BootLogoProblem.TooManyPixels, BootLogoRules.Check(Pictures.Jpeg(769, 432), screen, out _));
        Assert.Equal(BootLogoProblem.TooManyPixels, BootLogoRules.Check(Pictures.Gif(768, 433), screen, out _));
        Assert.Equal(BootLogoProblem.ColorDepth, BootLogoRules.Check(Pictures.Jpeg(100, 100, components: 4), screen, out _));
        Assert.Equal(BootLogoProblem.NotGifOrJpeg, BootLogoRules.Check([1, 2, 3], screen, out _));
        // The size isn't checked while the screen is unknown.
        Assert.Equal(BootLogoProblem.None, BootLogoRules.Check(Pictures.Jpeg(4000, 3000), null, out _));
    }

    [Fact]
    public void Files_over_35_mb_are_refused()
    {
        var data = new byte[BootLogoRules.MaxBytes + 1];
        Pictures.Gif(100, 100).CopyTo(data, 0);
        Assert.Equal(BootLogoProblem.FileTooLarge, BootLogoRules.Check(data, new PixelSize(1920, 1080), out _));
    }
}

public sealed class BootLogoFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));

    private string Oem => Path.Combine(_root, "EFI", "OEM");

    [Fact]
    public void A_new_picture_replaces_the_old_one_whatever_its_format()
    {
        var folder = new BootLogoFolder(_root, () => null);
        var gif = Pictures.Gif(100, 100);
        Assert.Equal(BootLogoResult.Done, folder.Write(BootLogoImage.Read(gif)!, gif));
        Assert.Equal(BootLogoFormat.Gif, folder.Read()!.Value.Format);

        var jpeg = Pictures.Jpeg(200, 100);
        Assert.Equal(BootLogoResult.Done, folder.Write(BootLogoImage.Read(jpeg)!, jpeg));

        Assert.Equal(["AcerLogo.jpg"], Directory.GetFiles(Oem).Select(Path.GetFileName));
        Assert.Equal(jpeg, folder.Read()!.Value.Data);
    }

    [Fact]
    public void Not_enough_space_leaves_the_old_logo()
    {
        long free = long.MaxValue;
        var folder = new BootLogoFolder(_root, () => free);
        var gif = Pictures.Gif(100, 100);
        folder.Write(BootLogoImage.Read(gif)!, gif);

        free = 1000;
        var jpeg = Pictures.Jpeg(200, 100);
        Assert.Equal(BootLogoResult.NoSpace, folder.Write(BootLogoImage.Read(jpeg)!, jpeg));
        Assert.Equal(["AcerLogo.gif"], Directory.GetFiles(Oem).Select(Path.GetFileName));
    }

    [Fact]
    public void Restoring_removes_the_picture()
    {
        var folder = new BootLogoFolder(_root, () => null);
        var gif = Pictures.Gif(100, 100);
        folder.Write(BootLogoImage.Read(gif)!, gif);

        Assert.True(folder.Restore());
        Assert.Null(folder.Read());
        Assert.True(folder.Restore()); // nothing left to remove
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

public sealed class StartupEngineTests : IDisposable
{
    private static readonly AcerSmbios CustomLogoSmbios = new(2, 0x53, [new AcerSmbiosRecord((byte)GamingRecord.CustomBootLogo, 1)], []);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));

    private OpenSenseEngine Start(SimulatedMachine machine)
    {
        var engine = new OpenSenseEngine(machine, Path.Combine(_directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        engine.Start();
        return engine;
    }

    [Fact]
    public async Task Boot_animation_is_read_and_written_in_the_firmware()
    {
        using var engine = Start(new SimulatedMachine(SimulatedModel.Nitro2021));
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.True(snapshot.Capabilities.BootAnimation);
        Assert.True(snapshot.Firmware!.BootAnimation);

        Assert.True(await engine.SetBootAnimationAsync(false, TestContext.Current.CancellationToken));
        Assert.False((await engine.GetSnapshotAsync(TestContext.Current.CancellationToken)).Firmware!.BootAnimation);
    }

    [Fact]
    public async Task No_custom_logo_without_the_firmware_record_or_the_setup_switch()
    {
        using var engine = Start(new SimulatedMachine(SimulatedModel.Nitro2021)
        {
            SystemPartition = Path.Combine(_directory, "esp"),
            CustomBootLogoSwitch = false,
        });

        var caps = (await engine.GetSnapshotAsync(TestContext.Current.CancellationToken)).Capabilities;
        Assert.False(caps.CustomBootLogo);
        Assert.False(caps.CustomBootLogoSwitch);
        Assert.Equal(BootLogoResult.Unsupported, await engine.SetBootLogoAsync(Pictures.Gif(100, 100), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_setup_switch_alone_offers_the_custom_logo_and_is_turned_on_with_one()
    {
        // The AN515-57: SMBIOS says no custom logo, but its BIOS setup has "Customize POST animation" (misc setting 8).
        var machine = new SimulatedMachine(SimulatedModel.Nitro2021) { SystemPartition = Path.Combine(_directory, "esp"), CustomBootLogoShown = false };
        using var engine = Start(machine);
        var ct = TestContext.Current.CancellationToken;
        var caps = (await engine.GetSnapshotAsync(ct)).Capabilities;
        Assert.True(caps.CustomBootLogoSwitch);
        Assert.True(caps.CustomBootLogo);
        Assert.False(machine.Laptop!.CustomBootLogoShown);

        Assert.Equal(BootLogoResult.Done, await engine.SetBootLogoAsync(Pictures.Gif(100, 100), ct));
        Assert.True(machine.Laptop.CustomBootLogoShown);
    }

    [Fact]
    public async Task A_jpeg_the_firmware_cant_decode_is_refused()
    {
        var machine = new SimulatedMachine(SimulatedModel.Nitro2021) { Smbios = CustomLogoSmbios, SystemPartition = Path.Combine(_directory, "esp") };
        using var engine = Start(machine);

        Assert.Equal(BootLogoResult.NotBaseline, await engine.SetBootLogoAsync(Pictures.Jpeg(700, 400, frame: 0xC2), TestContext.Current.CancellationToken));
        Assert.False((await engine.GetBootLogoAsync(TestContext.Current.CancellationToken)).Custom);
    }

    [Fact]
    public async Task No_custom_logo_without_an_efi_system_partition()
    {
        using var engine = Start(new SimulatedMachine(SimulatedModel.Nitro2021) { Smbios = CustomLogoSmbios });

        Assert.False((await engine.GetSnapshotAsync(TestContext.Current.CancellationToken)).Capabilities.CustomBootLogo);
    }

    [Fact]
    public async Task A_custom_logo_is_checked_written_and_removed()
    {
        var esp = Path.Combine(_directory, "esp");
        var machine = new SimulatedMachine(SimulatedModel.Nitro2021) { Smbios = CustomLogoSmbios, SystemPartition = esp };
        using var engine = Start(machine);
        var ct = TestContext.Current.CancellationToken;
        Assert.True((await engine.GetSnapshotAsync(ct)).Capabilities.CustomBootLogo);
        Assert.Equal(new BootLogoState(false, null, null, new PixelSize(1920, 1080)), await engine.GetBootLogoAsync(ct));

        Assert.Equal(BootLogoResult.TooManyPixels, await engine.SetBootLogoAsync(Pictures.Jpeg(800, 400), ct));
        Assert.Equal(BootLogoResult.NotGifOrJpeg, await engine.SetBootLogoAsync([0x42, 0x4D, 0, 0], ct)); // a BMP

        var jpeg = Pictures.Jpeg(700, 400);
        Assert.Equal(BootLogoResult.Done, await engine.SetBootLogoAsync(jpeg, ct));
        Assert.True(File.Exists(Path.Combine(esp, "EFI", "OEM", "AcerLogo.jpg")));
        var state = await engine.GetBootLogoAsync(ct);
        Assert.True(state.Custom);
        Assert.Equal(BootLogoFormat.Jpeg, state.Format);
        Assert.Equal(jpeg, state.Image);

        Assert.True(await engine.RestoreBootLogoAsync(ct));
        Assert.False((await engine.GetBootLogoAsync(ct)).Custom);
    }

    [Fact]
    public async Task The_size_must_be_checkable()
    {
        var machine = new SimulatedMachine(SimulatedModel.Nitro2021) { Smbios = CustomLogoSmbios, SystemPartition = Path.Combine(_directory, "esp") };
        using var engine = Start(machine);
        machine.Screen = null; // e.g. the lid is closed

        Assert.Equal(BootLogoResult.ScreenUnknown, await engine.SetBootLogoAsync(Pictures.Gif(100, 100), TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
