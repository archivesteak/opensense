using System.Management;
using Windows.Win32;

namespace OpenSense.Core.Hardware.Boot;

public enum BootLogoResult
{
    Done,

    /// <summary>This laptop has no custom boot logo, or the EFI system partition wasn't found.</summary>
    Unsupported,

    NotGifOrJpeg,
    FileTooLarge,
    TooManyPixels,
    ColorDepth,

    /// <summary>A JPEG the firmware can't decode (see <see cref="BootLogoImage.Baseline"/>).</summary>
    NotBaseline,

    /// <summary>The internal screen's resolution couldn't be read (e.g. the lid is closed), so the size can't be checked.</summary>
    ScreenUnknown,

    /// <summary>Not enough free space on the EFI system partition.</summary>
    NoSpace,

    WriteFailed,
}

/// <summary>The boot logo now in use, and the screen it must fit.</summary>
/// <param name="Custom">A picture replaces Acer's logo.</param>
/// <param name="Format">The custom picture's format.</param>
/// <param name="Image">The custom picture, unless it is larger than <see cref="BootLogoRules.MaxBytes"/>.</param>
/// <param name="Screen">The internal screen's native resolution, when known.</param>
public sealed record BootLogoState(bool Custom, BootLogoFormat? Format, byte[]? Image, PixelSize? Screen)
{
    public static BootLogoState None { get; } = new(false, null, null, null);
}

/// <summary>The pictures the firmware shows at power-on, kept in <c>\EFI\OEM</c> on the EFI system partition.</summary>
public interface IBootLogoStore
{
    /// <summary>The custom picture, or null while Acer's logo shows.</summary>
    (BootLogoFormat Format, byte[]? Data)? Read();

    BootLogoResult Write(BootLogoImage image, byte[] data);

    /// <summary>Removes the custom picture, so Acer's logo shows again. False if a file couldn't be deleted.</summary>
    bool Restore();
}

/// <summary>
/// The <c>\EFI\OEM</c> folder under <paramref name="root"/>. A new picture is written and flushed next to the old one
/// before the old one is removed, so running out of space or a failed write leaves the previous logo in place.
/// </summary>
public sealed class BootLogoFolder(string root, Func<long?> freeSpace) : IBootLogoStore
{
    private const string TempName = "AcerLogo.new";

    /// <summary>Room left for the file system's own bookkeeping.</summary>
    private const long SpaceMargin = 1024 * 1024;

    private static readonly BootLogoFormat[] Formats = [BootLogoFormat.Gif, BootLogoFormat.Jpeg];

    public string Folder { get; } = Path.Combine(root, "EFI", "OEM");

    /// <summary>The EFI system partition Windows boots from (it has no drive letter), or null when there is none.</summary>
    public static BootLogoFolder? OpenSystemPartition(Action<string> log)
    {
        if (FindSystemPartition(log) is not { } volume)
            return null;
        return new BootLogoFolder(volume, () => FreeSpace(volume));
    }

    public (BootLogoFormat Format, byte[]? Data)? Read()
    {
        foreach (var format in Formats)
        {
            var file = new FileInfo(Path.Combine(Folder, BootLogoImage.FileNameFor(format)));
            if (!file.Exists)
                continue;
            return (format, file.Length <= BootLogoRules.MaxBytes ? File.ReadAllBytes(file.FullName) : null);
        }
        return null;
    }

    public BootLogoResult Write(BootLogoImage image, byte[] data)
    {
        var temp = Path.Combine(Folder, TempName);
        try
        {
            Directory.CreateDirectory(Folder);
            DeleteIfPresent(temp);
            if (freeSpace() is { } free && free < data.Length + SpaceMargin)
                return BootLogoResult.NoSpace;
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            // The firmware takes whichever picture it finds; there must be only one.
            foreach (var format in Formats)
                DeleteIfPresent(Path.Combine(Folder, BootLogoImage.FileNameFor(format)));
            File.Move(temp, Path.Combine(Folder, image.FileName));
            return BootLogoResult.Done;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                DeleteIfPresent(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }
            return BootLogoResult.WriteFailed;
        }
    }

    public bool Restore()
    {
        try
        {
            foreach (var format in Formats)
                DeleteIfPresent(Path.Combine(Folder, BootLogoImage.FileNameFor(format)));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// The partition Windows marks as its system partition, if it is an EFI system partition, as a volume path
    /// (<c>\\?\Volume{...}\</c>). Acer's software looks for a volume labelled "ESP", which only its factory image sets.
    /// </summary>
    private static string? FindSystemPartition(Action<string> log)
    {
        const string EspType = "{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}";
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\Microsoft\Windows\Storage"),
                new ObjectQuery("SELECT DiskNumber, PartitionNumber, GptType, IsSystem, AccessPaths FROM MSFT_Partition"));
            foreach (var partition in searcher.Get())
            {
                using (partition)
                {
                    if (partition["IsSystem"] is not true || !string.Equals(partition["GptType"] as string, EspType, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var volume = (partition["AccessPaths"] as string[] ?? [])
                        .FirstOrDefault(p => p.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase));
                    log($"EFI system partition: disk {partition["DiskNumber"]} partition {partition["PartitionNumber"]}, {volume ?? "no volume path"}");
                    if (volume is not null)
                        return volume;
                }
            }
            log("EFI system partition: none");
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            log($"EFI system partition: {ex.Message}");
        }
        return null;
    }

    private static long? FreeSpace(string volume) =>
        PInvoke.GetDiskFreeSpaceEx(volume, out var available, out _, out _) ? (long)available : null;
}
