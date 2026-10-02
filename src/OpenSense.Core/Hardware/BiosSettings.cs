using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenSense.Core.Hardware;

/// <summary>What a BIOS setting is: a choice between the values the BIOS lists, or one of its two passwords.</summary>
public enum BiosSettingKind
{
    Choice,
    Password,
}

/// <summary>How much a wrong change can cost (see <see cref="BiosProtocol.RiskOf"/>).</summary>
public enum BiosRisk
{
    Normal,

    /// <summary>A wrong value can stop the laptop from starting, or cut it off from its keyboard, drive or ports.</summary>
    Dangerous,
}

/// <summary>One BIOS setting as the firmware lists it, with the words the BIOS gives it.</summary>
/// <param name="Current">The value now set; not among <paramref name="Options"/> when the BIOS shows none (its own word for that).</param>
/// <param name="Options">The values it accepts, in the BIOS's order; empty for a password.</param>
public sealed record BiosSetting(string Name, BiosSettingKind Kind, string Current, IReadOnlyList<string> Options, BiosRisk Risk);

/// <summary>What became of a change to the BIOS settings.</summary>
public enum BiosChangeResult
{
    Done,

    /// <summary>Stored; the BIOS applies it when the laptop starts again (what every stored change reports).</summary>
    RestartNeeded,

    /// <summary>OpenSense's own switches for BIOS settings are off, or this one is a dangerous one and they don't allow those.</summary>
    NotAllowed,

    /// <summary>This laptop or its BIOS does not offer the setting to Windows.</summary>
    Unavailable,

    /// <summary>The BIOS does not do this (for a saved set of user defaults: none saved).</summary>
    Unsupported,

    WrongPassword,

    /// <summary>The BIOS did not accept the name or the value.</summary>
    Rejected,

    /// <summary>The wrong password was tried too often; the BIOS listens again after the next start.</summary>
    TooManyAttempts,

    Failed,
}

/// <summary>
/// Whether the user lets OpenSense change BIOS settings, kept with the machine settings and checked by the engine, so
/// no client can go around it.
/// </summary>
public sealed record BiosAccess
{
    /// <summary>The BIOS settings can be seen and changed, apart from the dangerous ones.</summary>
    public bool Enabled { get; init; }

    /// <summary>The dangerous ones too (only while <see cref="Enabled"/>).</summary>
    public bool Dangerous { get; init; }

    public bool Allows(BiosRisk risk) => Enabled && (risk == BiosRisk.Normal || Dangerous);
}

/// <summary>
/// Acer's BIOS settings interface (root\WMI, from the 2025 Predators on; the PHN16-73's MOF is version 2.94): the data class
/// <c>ListBIOSSettings</c> has one string per setting and <c>MethodForPowerShell</c> changes them, both with the BIOS
/// supervisor password when one is set (an empty one otherwise). Pure functions.
/// </summary>
public static partial class BiosProtocol
{
    public const string MethodClass = "MethodForPowerShell";
    public const string ListClass = "ListBIOSSettings";
    public const string ListProperty = "CurrentSetting";

    public const string SetMethod = "SetBiosSetting";
    public const string LoadDefaultsMethod = "LoadBIOSDefault";
    public const string LoadUserDefaultsMethod = "LoadUserDefault";

    /// <summary>The setting that sets the supervisor password; its new value is the password.</summary>
    public const string SupervisorPassword = "Set Supervisor Password";

    /// <summary>The setting that sets the user password; its new value is the password.</summary>
    public const string UserPassword = "Set User Password";

    /// <summary>How long a password or a setting's name and value can be: the BIOS copies each into a buffer of its own.</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// The settings whose wrong value costs nothing that switching back doesn't undo: the devices, wake-up and power
    /// options, the keyboard's and sound's conveniences and the passwords, as the BIOS setup treats them. Every other
    /// setting counts as dangerous: a wrong value can stop the laptop from starting or cut it off from its keyboard, drive
    /// or ports (the storage modes and ports, the USB, Thunderbolt and Type-C controllers, the keyboard's interface, the
    /// graphics mode), change what the BIOS trusts or locks, or can never be undone (Absolute's module). A name the BIOS
    /// adds later is not known to be safe.
    /// </summary>
    private static readonly HashSet<string> Safe = new(StringComparer.OrdinalIgnoreCase)
    {
        "Intel VTX", "Intel VTD", "Trusted Execution Technology", "AMD-IOMMU", "AMD-SVM", "Above 4G decoding", "Re-Size BAR Support",
        "Active Efficient Cores", "GNA Device", "Dash Support", "KVM Feature", "AIMT-Support", "Wi-Fi", "WiGig", "Bluetooth", "WWAN",
        "Card Reader", "Wired LAN", "Audio", "Camera", "Fingerprint", "TBT Detection Gain", "Optical Drive", "Speader & Headphone",
        "Microphone", "CNVi WLAN/BT", "Privacy Screen", "Network Boot", "Wake on LAN", "Lid Open Resume", "Wake on WLAN",
        "Power-off USB Charge", "Battery Threshold", "USB Boot", "TBT Wake", "Wake on USB while lid closed", "TBT Wake from S4 Support",
        "USB Wake from S4 Support", "Wake on LAN from Dock", SupervisorPassword, UserPassword, "Password on Boot", "F12 Boot Menu",
        "Function Key behavior", "D2D Recovery", "Keyboard backlight timeout", "Internal KB Numpad", "Fast Boot", "POST Animation & Sound",
        "Sound",
    };

    public static BiosRisk RiskOf(string name) => Safe.Contains(name) ? BiosRisk.Normal : BiosRisk.Dangerous;

    public static bool IsPassword(string name) =>
        name.Equals(SupervisorPassword, StringComparison.OrdinalIgnoreCase) || name.Equals(UserPassword, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The settings in the <c>ListBIOSSettings</c> strings, in their order. A setting this laptop lacks has an empty
    /// string (or none the BIOS could list); those, and anything not in the form "Name, Current[Value;Value;]", are left out.
    /// </summary>
    public static IReadOnlyList<BiosSetting> ParseList(IEnumerable<string> lines) =>
        [.. lines.Select(ParseSetting).OfType<BiosSetting>()];

    /// <summary>
    /// One setting from "Name, Current[Value;Value;]". A password has no values: the BIOS lists it as "Name, Unsupported".
    /// </summary>
    public static BiosSetting? ParseSetting(string line)
    {
        if (SettingLine().Match(line) is not { Success: true } match)
            return null;
        var name = match.Groups["name"].Value.Trim();
        var current = match.Groups["current"].Value.Trim();
        if (name.Length == 0)
            return null;
        if (IsPassword(name))
            return new BiosSetting(name, BiosSettingKind.Password, "", [], RiskOf(name));

        var options = match.Groups["options"] is { Success: true } listed
            ? listed.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        return options.Length == 0 ? null : new BiosSetting(name, BiosSettingKind.Choice, current, options, RiskOf(name));
    }

    /// <summary>
    /// What a method's <c>Return</c> text says: it starts with the status, as in "0x02 - Incorrect Password"
    /// (0 done, 8 done once the laptop restarts, 1 not supported, 2 wrong password, 3 wrong parameters, 7 too many tries).
    /// </summary>
    public static BiosChangeResult Result(string? text)
    {
        if (text is null || StatusText().Match(text) is not { Success: true } match)
            return BiosChangeResult.Failed;
        return int.Parse(match.Groups["code"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) switch
        {
            0 => BiosChangeResult.Done,
            8 => BiosChangeResult.RestartNeeded,
            1 => BiosChangeResult.Unsupported,
            2 => BiosChangeResult.WrongPassword,
            3 => BiosChangeResult.Rejected,
            7 => BiosChangeResult.TooManyAttempts,
            _ => BiosChangeResult.Failed,
        };
    }

    public static bool Succeeded(BiosChangeResult result) => result is BiosChangeResult.Done or BiosChangeResult.RestartNeeded;

    /// <summary>
    /// Text the BIOS can take: it keeps one byte of each character, so anything but plain printable ASCII would arrive as
    /// something else. Empty is fine only for <paramref name="allowEmpty"/> (the supervisor password when none is set).
    /// </summary>
    public static bool IsPlainText(string text, bool allowEmpty = false) =>
        (allowEmpty || text.Length > 0) && text.Length <= MaxLength && text.All(c => c is >= ' ' and <= '~');

    [GeneratedRegex(@"^(?<name>.+?),\s*(?<current>[^\[\]]*?)\s*(?:\[(?<options>[^\]]*)\])?\s*$")]
    private static partial Regex SettingLine();

    [GeneratedRegex(@"^\s*0x(?<code>[0-9A-Fa-f]{1,2})(?![0-9A-Fa-f])")]
    private static partial Regex StatusText();
}
