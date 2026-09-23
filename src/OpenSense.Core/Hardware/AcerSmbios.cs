using System.Buffers.Binary;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace OpenSense.Core.Hardware;

/// <summary>A 3-byte record in the Acer gaming SMBIOS structure (type 0xAC).</summary>
public readonly record struct AcerSmbiosRecord(byte Id, ushort Value);

/// <summary>
/// Acer's OEM SMBIOS structures. Readable without elevation.
/// Type 0xAC carries the gaming interface version (which selects some firmware encodings);
/// type 0xAA lists hotkey functions (e.g. the keyboard-backlight function 0x84).
/// </summary>
public sealed record AcerSmbios(
    byte? GamingMajor,
    byte? GamingMinor,
    IReadOnlyList<AcerSmbiosRecord> GamingRecords,
    IReadOnlyList<AcerSmbiosRecord> HotkeyFunctions)
{
    public static AcerSmbios Empty { get; } = new(null, null, [], []);

    /// <summary>E.g. 2.83 on AN515-57 (bytes 02 53).</summary>
    public double? GamingVersion => GamingMajor is { } major && GamingMinor is { } minor ? major + minor / 100.0 : null;

    /// <summary>Firmware from interface 2.86 on takes <c>SetGamingLED</c> as a byte array.</summary>
    public bool UsesArrayLedBehavior => GamingMajor is { } major && GamingMinor is { } minor && (major > 2 || (major == 2 && minor >= 0x56));

    public bool HasHotkeyFunction(byte function) => HotkeyFunctions.Any(r => r.Id == function);

    public static unsafe AcerSmbios Read()
    {
        const uint rsmb = 0x52534D42; // 'RSMB'
        var provider = (FIRMWARE_TABLE_PROVIDER)rsmb;
        var size = PInvoke.GetSystemFirmwareTable(provider, 0, null, 0);
        if (size == 0)
            return Empty;

        var buffer = new byte[size];
        fixed (byte* raw = buffer)
        {
            if (PInvoke.GetSystemFirmwareTable(provider, 0, raw, size) != size)
                return Empty;
        }

        // RawSMBIOSData header: 4 bytes of version info, then the table length.
        var length = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4)), (uint)(buffer.Length - 8));
        return Parse(buffer.AsSpan(8, length));
    }

    internal static AcerSmbios Parse(ReadOnlySpan<byte> table)
    {
        byte? major = null, minor = null;
        var gaming = new List<AcerSmbiosRecord>();
        var hotkeys = new List<AcerSmbiosRecord>();

        var offset = 0;
        while (offset + 4 <= table.Length)
        {
            var type = table[offset];
            var formattedLength = table[offset + 1];
            if (formattedLength < 4 || offset + formattedLength > table.Length)
                break;
            var formatted = table.Slice(offset, formattedLength);

            if (type == 0xAC && formatted.Length >= 6)
            {
                major = formatted[4];
                minor = formatted[5];
                for (var i = 6; i + 3 <= formatted.Length; i += 3)
                    gaming.Add(new AcerSmbiosRecord(formatted[i], BinaryPrimitives.ReadUInt16LittleEndian(formatted[(i + 1)..])));
            }
            else if (type == 0xAA)
            {
                // After a 14-byte header come 4-byte records: function, 0x02, u16 value.
                for (var i = 14; i + 4 <= formatted.Length; i += 4)
                {
                    if (formatted[i + 1] == 0x02)
                        hotkeys.Add(new AcerSmbiosRecord(formatted[i], BinaryPrimitives.ReadUInt16LittleEndian(formatted[(i + 2)..])));
                }
            }

            // Skip the string set, which ends with a double NUL.
            var next = offset + formattedLength;
            while (next + 1 < table.Length && (table[next] != 0 || table[next + 1] != 0))
                next++;
            offset = next + 2;
            if (type == 127) // end-of-table
                break;
        }

        return new AcerSmbios(major, minor, gaming, hotkeys);
    }
}
