using System.Buffers.Binary;

namespace OpenSense.Core.Hardware.Boot;

public enum BootLogoFormat
{
    Gif,
    Jpeg,
}

public readonly record struct PixelSize(int Width, int Height);

/// <summary>A picture's format, size and colour depth, read from its header.</summary>
public sealed record BootLogoImage(BootLogoFormat Format, int Width, int Height, int BitsPerPixel)
{
    /// <summary>
    /// The firmware's decoder takes it (AN515-57 V1.17, Insyde's decoder). Its JPEG decoder knows only baseline JPEGs
    /// (SOF0) of 8 bits with 1 or 3 components, each sampled 1, 2 or 4 times per block and 10 at most in all, 8-bit
    /// quantization tables and Huffman tables 0 and 1, and a single scan of all components with ids 0-3; it turns down
    /// progressive, extended and arithmetic-coded ones. It also reads markers to the end of the file and keeps the last
    /// frame, tables and scan it finds, so nothing may follow the scan: no second scan, no picture appended after the
    /// end (as phones add previews). GIFs are always fine.
    /// </summary>
    public bool Baseline { get; init; } = true;

    /// <summary>The name the firmware looks for in <c>\EFI\OEM</c>.</summary>
    public string FileName => FileNameFor(Format);

    public static string FileNameFor(BootLogoFormat format) => format == BootLogoFormat.Gif ? "AcerLogo.gif" : "AcerLogo.jpg";

    /// <summary>Null unless <paramref name="data"/> starts like a GIF or a JPEG with a frame header.</summary>
    public static BootLogoImage? Read(ReadOnlySpan<byte> data) => ReadGif(data) ?? ReadJpeg(data);

    private static BootLogoImage? ReadGif(ReadOnlySpan<byte> data)
    {
        if (data.Length < 13 || !(data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
            return null;
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
        // Every pixel is an index into a palette of at most 256 colours.
        return width > 0 && height > 0 ? new BootLogoImage(BootLogoFormat.Gif, width, height, 8) : null;
    }

    /// <summary>
    /// The frame header's size and depth, and whether the firmware decodes it; null without a frame header before the
    /// image data. A JPEG whose structure breaks after its frame header counts as one the firmware can't decode.
    /// </summary>
    private static BootLogoImage? ReadJpeg(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
            return null;
        BootLogoImage? image = null;
        var components = 0;
        var decodable = true;
        var i = 2;
        while (i + 4 <= data.Length)
        {
            if (data[i] != 0xFF)
                return Undecodable(image);
            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++; // fill byte
                continue;
            }
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2; // markers without a length
                continue;
            }
            if (marker == 0xD9)
                return Undecodable(image); // the image ends before its data
            int length = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 2)..]);
            if (length < 2 || i + 2 + length > data.Length)
                return Undecodable(image);
            var segment = data.Slice(i + 4, length - 2);
            if (marker == 0xDA)
            {
                return image is null ? null : image with
                {
                    Baseline = decodable && ScanFits(segment, components) && OnlyCodedData(data[(i + 2 + length)..]),
                };
            }
            if (IsFrameHeader(marker))
            {
                if (image is not null)
                {
                    decodable = false; // a second frame header: the firmware would take that one
                }
                else
                {
                    if (length < 8)
                        return null;
                    var precision = segment[0];
                    int height = BinaryPrimitives.ReadUInt16BigEndian(segment[1..]);
                    int width = BinaryPrimitives.ReadUInt16BigEndian(segment[3..]);
                    components = segment[5];
                    if (width == 0 || height == 0 || components == 0)
                        return null;
                    image = new BootLogoImage(BootLogoFormat.Jpeg, width, height, precision * components);
                    decodable &= marker == 0xC0 && precision == 8 && components is 1 or 3
                        && segment.Length - 6 >= 3 * components && SamplingFits(segment.Slice(6, 3 * components));
                }
            }
            else if (marker == 0xDB)
            {
                decodable &= QuantizationFits(segment);
            }
            else if (marker == 0xC4)
            {
                decodable &= HuffmanFits(segment);
            }
            i += 2 + length;
        }
        return Undecodable(image); // no image data
    }

    private static BootLogoImage? Undecodable(BootLogoImage? image) => image is null ? null : image with { Baseline = false };

    /// <summary>SOF0-SOF15, except DHT (C4), JPG (C8) and DAC (CC), which share the range.</summary>
    private static bool IsFrameHeader(byte marker) => marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    /// <summary>DQT: tables of 8-bit values (65 bytes each), numbered 0-3.</summary>
    private static bool QuantizationFits(ReadOnlySpan<byte> segment)
    {
        if (segment.Length == 0 || segment.Length % 65 != 0)
            return false;
        for (var t = 0; t < segment.Length; t += 65)
        {
            if (segment[t] >> 4 != 0 || (segment[t] & 0xF) > 3)
                return false;
        }
        return true;
    }

    /// <summary>DHT: DC and AC tables numbered 0 or 1, each 16 code counts and at most 256 values.</summary>
    private static bool HuffmanFits(ReadOnlySpan<byte> segment)
    {
        var t = 0;
        while (t < segment.Length)
        {
            if (t + 17 > segment.Length || segment[t] >> 4 > 1 || (segment[t] & 0xF) > 1)
                return false;
            var values = 0;
            foreach (var count in segment.Slice(t + 1, 16))
                values += count;
            if (values > 256)
                return false;
            t += 17 + values;
        }
        return t == segment.Length;
    }

    /// <summary>
    /// SOS: all the frame's components in one scan (ids 0-3, Huffman tables 0 or 1), over the whole spectrum without
    /// successive approximation (Ss 0, Se 63, Ah and Al 0).
    /// </summary>
    private static bool ScanFits(ReadOnlySpan<byte> segment, int components)
    {
        if (segment.Length == 0 || segment[0] != components || segment.Length != 1 + 2 * components + 3)
            return false;
        for (var c = 0; c < components; c++)
        {
            var tables = segment[2 + 2 * c];
            if (segment[1 + 2 * c] > 3 || tables >> 4 > 1 || (tables & 0xF) > 1)
                return false;
        }
        return segment[^3] == 0 && segment[^2] == 63 && segment[^1] == 0;
    }

    /// <summary>
    /// What follows the scan's header holds no marker but restarts and the end (FF only before 00, fill bytes, RSTn or
    /// EOI), up to the end of the file.
    /// </summary>
    private static bool OnlyCodedData(ReadOnlySpan<byte> rest)
    {
        for (var i = 0; i + 1 < rest.Length; i++)
        {
            if (rest[i] == 0xFF && rest[i + 1] is not (0x00 or 0xFF or 0xD9 or (>= 0xD0 and <= 0xD7)))
                return false;
        }
        return true;
    }

    /// <summary>
    /// The frame header's components (id, sampling, table; three bytes each): the firmware wants each one's horizontal ×
    /// vertical sampling to be 1, 2 or 4, and 10 at most for all of them.
    /// </summary>
    private static bool SamplingFits(ReadOnlySpan<byte> components)
    {
        if (components.Length == 0 || components.Length % 3 != 0)
            return false;
        var total = 0;
        for (var c = 0; c < components.Length; c += 3)
        {
            var blocks = (components[c + 1] >> 4) * (components[c + 1] & 0xF);
            if (blocks is not (1 or 2 or 4))
                return false;
            total += blocks;
        }
        return total <= 10;
    }
}

public enum BootLogoProblem
{
    None,

    /// <summary>Neither a GIF nor a JPEG.</summary>
    NotGifOrJpeg,

    /// <summary>More than <see cref="BootLogoRules.MaxBytes"/>.</summary>
    FileTooLarge,

    /// <summary>Wider or taller than <see cref="BootLogoRules.MaxSize"/>.</summary>
    TooManyPixels,

    /// <summary>32 bits per pixel or more (e.g. a CMYK JPEG).</summary>
    ColorDepth,

    /// <summary>A JPEG the firmware can't decode, e.g. a progressive one (see <see cref="BootLogoImage.Baseline"/>).</summary>
    NotBaseline,
}

/// <summary>What Acer's software accepts as a boot logo.</summary>
public static class BootLogoRules
{
    public const long MaxBytes = 35_000_000;

    public const int MaxBitsPerPixel = 31;

    /// <summary>At most 40 % of the internal screen's width and height.</summary>
    public static PixelSize MaxSize(PixelSize screen) => new(screen.Width * 2 / 5, screen.Height * 2 / 5);

    /// <param name="screen">The internal screen's native resolution; the size isn't checked when it is unknown.</param>
    public static BootLogoProblem Check(ReadOnlySpan<byte> data, PixelSize? screen, out BootLogoImage? image)
    {
        image = BootLogoImage.Read(data);
        if (image is null)
            return BootLogoProblem.NotGifOrJpeg;
        if (data.Length > MaxBytes)
            return BootLogoProblem.FileTooLarge;
        if (screen is { } s && MaxSize(s) is var max && (image.Width > max.Width || image.Height > max.Height))
            return BootLogoProblem.TooManyPixels;
        if (image.BitsPerPixel > MaxBitsPerPixel)
            return BootLogoProblem.ColorDepth;
        if (!image.Baseline)
            return BootLogoProblem.NotBaseline;
        return BootLogoProblem.None;
    }
}
