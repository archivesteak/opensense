using System.Globalization;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace OpenSense.Core.Hardware;

/// <summary>An ACPI table as the firmware gave it to Windows.</summary>
/// <param name="Name">Windows' name for it: its signature, or SSD1, SSD2… for the SSDTs.</param>
public sealed record AcpiTable(string Name, byte[] Data)
{
    /// <summary>
    /// Every table, from the copies Windows keeps under <c>HKLM\HARDWARE\ACPI</c> (<c>GetSystemFirmwareTable</c> returns
    /// only the first SSDT); empty where they can't be read.
    /// </summary>
    public static IReadOnlyList<AcpiTable> ReadAll()
    {
        var tables = new List<AcpiTable>();
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"HARDWARE\ACPI");
            foreach (var name in root?.GetSubKeyNames() ?? [])
            {
                using var key = root!.OpenSubKey(name);
                if (key is not null)
                    Collect(name, key, tables);
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        return tables;
    }

    // A table is the value "00000000" a few keys down (signature\OEM id\table id\revision).
    private static void Collect(string name, RegistryKey key, List<AcpiTable> tables)
    {
        if (key.GetValue("00000000") is byte[] data)
            tables.Add(new AcpiTable(name, data));
        foreach (var subName in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(subName);
            if (sub is not null)
                Collect(name, sub, tables);
        }
    }
}

/// <summary>
/// What the BIOS's own ACPI code does with an operating-mode write (<c>SetGamingMiscSetting</c>, setting 0x0B), read from
/// the gaming WMI method (<c>WMBH</c>, method 0x16): in how many places it acts on each mode value. A board or GPU variant
/// gets a branch of its own and every branch acts on Balanced, so a mode acted on in fewer places than Balanced does
/// nothing on some of them. The AN515-58's BIOS 2.19 lists Turbo on its IH51A board (misc 0x0A answers 0x33), but only one
/// of that board's three branches acts on it, and there it picks a Quiet power policy; the others keep the last mode's.
/// A place that only answers an error status (the AN515-46's Turbo) doesn't count. Firmware whose WMI code hands the
/// write to SMM has none of this in ACPI: <see cref="Find"/> answers null there.
/// </summary>
/// <param name="Table">The table the method is in, for the diagnostics.</param>
/// <param name="Places">Per mode value, the places in the code that act on it.</param>
public sealed record AcpiModeCode(string Table, IReadOnlyDictionary<byte, int> Places)
{
    private const byte MethodOp = 0x14, IfOp = 0xA0, LEqualOp = 0x93, ReturnOp = 0xA4;
    private const int SetMiscSetting = 0x16, OperatingModeSetting = 0x0B;

    /// <summary>The code acts on the mode wherever it acts on Balanced.</summary>
    public bool Handles(OperatingMode mode) =>
        Places.GetValueOrDefault((byte)mode) >= Places.GetValueOrDefault((byte)OperatingMode.Balanced);

    /// <summary>E.g. "Quiet 3, Balanced 3, Performance 3, Turbo 1".</summary>
    public string Describe() => string.Join(", ", Places.OrderBy(p => p.Key).Select(p =>
        (Enum.IsDefined((OperatingMode)p.Key) ? ((OperatingMode)p.Key).ToString() : "value " + p.Key.ToString(CultureInfo.InvariantCulture)) +
        " " + p.Value.ToString(CultureInfo.InvariantCulture)));

    /// <summary>The first table with the mode code; null where none has it.</summary>
    public static AcpiModeCode? Find(IEnumerable<AcpiTable> tables) =>
        tables.Select(t => Read(t.Name, t.Data)).FirstOrDefault(c => c is not null);

    private static AcpiModeCode? Read(string table, byte[] aml)
    {
        if (FindMethod(aml, "WMBH"u8) is not var (start, end))
            return null;
        var returned = ReturnedNames(aml, start, end);
        foreach (var set in Comparisons(aml, start, end).Where(c => c.Value == SetMiscSetting))
        {
            foreach (var setting in Comparisons(aml, set.Body, set.End).Where(c => c.Value == OperatingModeSetting))
            {
                // The mode is whatever the code compares with Quiet, Balanced and Performance (a local, or a Switch's temporary).
                var mode = Comparisons(aml, setting.Body, setting.End).GroupBy(c => c.Operand).FirstOrDefault(g =>
                    g.Any(c => c.Value == (int)OperatingMode.Quiet) && g.Any(c => c.Value == (int)OperatingMode.Balanced) &&
                    g.Any(c => c.Value == (int)OperatingMode.Performance));
                if (mode is null)
                    continue;
                var places = mode.Where(c => c.Value <= byte.MaxValue && Acts(aml, c.Body, c.End, returned))
                    .GroupBy(c => (byte)c.Value).ToDictionary(g => g.Key, g => g.Count());
                if (places.GetValueOrDefault((byte)OperatingMode.Balanced) > 0)
                    return new AcpiModeCode(table, places);
            }
        }
        return null;
    }

    /// <summary>An <c>If (operand == constant)</c> block: its statements start at Body and it ends at End.</summary>
    private readonly record struct Comparison(string Operand, int Value, int Body, int End);

    /// <summary>The method's statements, after its name and flags, and where it ends.</summary>
    private static (int Start, int End)? FindMethod(byte[] aml, ReadOnlySpan<byte> name)
    {
        for (var at = aml.AsSpan().IndexOf(name); at >= 0;)
        {
            // MethodOp, a PkgLength of one to four bytes, then the name.
            for (var size = 1; size <= 4; size++)
            {
                var op = at - 1 - size;
                if (op >= 0 && aml[op] == MethodOp && PkgLength(aml, op + 1) is { Size: var s, Length: var length } && s == size)
                {
                    var end = op + 1 + length;
                    if (end > at + 5 && end <= aml.Length)
                        return (at + 5, end);
                }
            }
            var next = aml.AsSpan(at + 1).IndexOf(name);
            at = next < 0 ? -1 : at + 1 + next;
        }
        return null;
    }

    /// <summary>Every <c>If (operand == constant)</c> block in [start, end), nested ones too.</summary>
    private static IEnumerable<Comparison> Comparisons(byte[] aml, int start, int end)
    {
        for (var at = start; at < end - 4; at++)
        {
            if (aml[at] != IfOp || PkgLength(aml, at + 1) is not { } length || at + 1 + length.Length > end)
                continue;
            // LEqual, an operand and a constant: three bytes at least.
            var predicate = at + 1 + length.Size;
            if (predicate + 3 > at + 1 + length.Length)
                continue;
            if (aml[predicate] != LEqualOp || Operand(aml, predicate + 1) is not { } operand)
                continue;
            if (Constant(aml, predicate + 1 + operand.Size) is not { } constant)
                continue;
            yield return new Comparison(operand.Name, constant.Value, predicate + 1 + operand.Size + constant.Size, at + 1 + length.Length);
        }
    }

    /// <summary>A local, an argument or a Switch's temporary (<c>_T_n</c>).</summary>
    private static (string Name, int Size)? Operand(byte[] aml, int at) => aml[at] switch
    {
        >= 0x60 and <= 0x67 => ("Local" + (aml[at] - 0x60), 1),
        >= 0x68 and <= 0x6E => ("Arg" + (aml[at] - 0x68), 1),
        (byte)'_' when at + 4 <= aml.Length && aml[at + 1] == 'T' && aml[at + 2] == '_' => (Encoding.ASCII.GetString(aml, at, 4), 4),
        _ => null,
    };

    private static (int Value, int Size)? Constant(byte[] aml, int at) => at >= aml.Length ? null : aml[at] switch
    {
        0x00 => (0, 1), // Zero
        0x01 => (1, 1), // One
        0x0A when at + 2 <= aml.Length => (aml[at + 1], 2),
        0x0B when at + 3 <= aml.Length => (aml[at + 1] | aml[at + 2] << 8, 3),
        _ => null,
    };

    private static (int Length, int Size)? PkgLength(byte[] aml, int at)
    {
        if (at >= aml.Length)
            return null;
        var lead = aml[at];
        var extra = lead >> 6;
        if (extra == 0)
            return (lead & 0x3F, 1);
        if (at + extra >= aml.Length)
            return null;
        var length = lead & 0x0F;
        for (var i = 0; i < extra; i++)
            length |= aml[at + 1 + i] << (4 + 8 * i);
        return (length, 1 + extra);
    }

    /// <summary>The names the method returns (its answer buffers): writing only those is answering, not acting.</summary>
    private static HashSet<string> ReturnedNames(byte[] aml, int start, int end)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var at = start; at < end - 4; at++)
        {
            if (aml[at] != ReturnOp)
                continue;
            var name = aml[at + 1] is (byte)'\\' or (byte)'^' ? at + 2 : at + 1;
            if (name + 4 <= end && IsNameSegment(aml, name))
                names.Add(Encoding.ASCII.GetString(aml, name, 4));
        }
        return names;
    }

    /// <summary>The statements name something other than the answer buffers and Switch temporaries: a variable, a method.</summary>
    private static bool Acts(byte[] aml, int start, int end, HashSet<string> returned)
    {
        for (var at = start; at + 4 <= end; at++)
        {
            if (!IsNameSegment(aml, at))
                continue;
            var name = Encoding.ASCII.GetString(aml, at, 4);
            if (!name.StartsWith("_T_", StringComparison.Ordinal) && !returned.Contains(name))
                return true;
        }
        return false;
    }

    private static bool IsNameSegment(byte[] aml, int at) =>
        aml[at] is >= (byte)'A' and <= (byte)'Z' or (byte)'_' && IsNameChar(aml[at + 1]) && IsNameChar(aml[at + 2]) && IsNameChar(aml[at + 3]);

    private static bool IsNameChar(byte b) => b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'_';
}
