using System.Text.RegularExpressions;

namespace OpenSense.Core.Hardware;

/// <summary>What the model's reviewed BIOS does with the keyboard's WMI methods (<c>Set/GetGamingKBBacklight</c>, <c>SetGamingRgbKb</c>).</summary>
public enum EcKeyboardControl
{
    /// <summary>Not reviewed: SMBIOS, the catalog and the firmware's answers decide.</summary>
    Default,

    /// <summary>The embedded controller drives a zoned RGB keyboard from them, whatever SMBIOS record 0x0A says.</summary>
    Rgb,

    /// <summary>The embedded controller passes the brightness on and nothing else.</summary>
    BrightnessOnly,

    /// <summary>Nothing reads what they store (the methods answer anyway).</summary>
    Unsupported,
}

/// <summary>
/// What a model's BIOS does, from its decompiled firmware (the version reviewed per model below): where the WMI methods
/// answer without anything behind them, and quirks that change how they are used. Nothing here comes from Acer's
/// software. Models not listed get the generic profile, and their firmware's answers decide.
/// </summary>
public sealed record AcerFirmwareProfile
{
    /// <summary>The reviewed model the name matched; null for the generic profile.</summary>
    public string? ModelCode { get; init; }

    /// <summary>
    /// False where <c>APGeAction</c> function 7 answers but isn't CoolBoost: nothing acts on it on the Nitro V 16s (a stub, or a
    /// register the EC never reads), and it is the operating mode on the NL16-71G (which its own answer says too).
    /// </summary>
    public bool? WmiCoolBoostSupported { get; init; }

    /// <summary>False where misc 0x0A/0x0B are refused or do nothing (the modes then go through the EC HID interface or function 7).</summary>
    public bool? WmiOperatingModesSupported { get; init; }

    /// <summary>
    /// The firmware moves the mode relative to the one in force (the AN515-57's ACPI steps Intel DTT's profile index), so a
    /// mode goes out from Balanced. The reviewed Nitro V 16s store the mode itself.
    /// </summary>
    public bool RequiresBalancedModeReset { get; init; } = true;

    /// <summary><c>GetGamingFanSpeed</c> echoes what was set (the ANV16S-61's answers the CPU fan's value for the GPU and fails for the CPU).</summary>
    public bool FanBoostReadbackReliable { get; init; } = true;

    /// <summary>Misc 0x0F switches the firmware's Fn lock (1 locked, 2 unlocked); elsewhere it is refused or does nothing.</summary>
    public bool SupportsFnLockProbe { get; init; }

    public EcKeyboardControl KeyboardControl { get; init; }

    /// <summary>
    /// Whether <c>SetGamingLED</c> switches keyboard zones; false where the method is a stub or the EC never reads the mask, so
    /// a zone that is off goes out black.
    /// </summary>
    public bool? KeyboardZoneSwitches { get; init; }

    /// <summary>The firmware puts its default red back over all-black static zones (the ANV16-41's ACPI, on every sensor read).</summary>
    public bool ResetsBlackStaticColors { get; init; }

    /// <summary>
    /// The longest BIOS password that can be typed at the next start: 16 unless the reviewed BIOS's password check takes
    /// 128 (a longer one set through WMI would be cut short, or refused when asked for).
    /// </summary>
    public int BiosPasswordMaxLength { get; init; } = 16;

    private static readonly IReadOnlyDictionary<string, string> ReviewedBios = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ANV16-41"] = "1.16", ["ANV16-71"] = "1.11", ["ANV16-72"] = "1.50", ["ANV16-I31"] = "1.08",
        ["ANV16-42"] = "1.15", ["ANV16-61"] = "1.15", ["ANV16-A31"] = "1.04", ["ANV16-A71"] = "1.04",
        ["ANV16S-41"] = "1.14", ["ANV16S-61"] = "1.14", ["ANV16S-71"] = "1.06", ["PHN16-73"] = "1.28",
        ["NL16-71G"] = "1.19",
    };

    public static AcerFirmwareProfile For(string? model, string? biosVersion = null)
    {
        var code = model is null ? null : ReviewedBios.Keys.FirstOrDefault(c =>
            Regex.IsMatch(model, $"(?<![A-Za-z0-9]){Regex.Escape(c)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        if (code is null)
            return new();

        var revision = biosVersion is null ? null : Regex.Match(biosVersion, @"(?<![\d.])(\d+\.\d+)(?![\d.])").Groups[1].Value;
        var predator = code == "PHN16-73";
        // The Nitro Lite's whole gaming interface is stubs in SMM but the temperatures; its modes are function 7's, which
        // says so itself, and its battery-boost flag is a constant 0, which never holds anything back.
        var lite = code == "NL16-71G";
        // Misc 0x0A/0x0B and the keyboard methods are stubs in SMM; the modes go through the EC HID interface or function 7.
        var noWmiModes = lite || code is "ANV16-72" or "ANV16-I31";
        // The record is stored but the EC reads none of it: the 71's keyboard colour follows the operating mode, the
        // KH62 boards' keyboard is the EC's own I2C one.
        var unsupported = noWmiModes || code is "ANV16-71" or "ANV16-42" or "ANV16-61";
        // The EC sends the lighting chip the brightness alone (the S71's the effect number too, without colours).
        var brightnessOnly = predator || code is "ANV16-A31" or "ANV16-A71" or "ANV16S-41" or "ANV16S-61" or "ANV16S-71";
        // The password check takes 128 characters here; the others take 16, or their setup does.
        var longPassword = code is "ANV16-72" or "ANV16-I31" or "ANV16S-71" or "PHN16-73" && revision == ReviewedBios[code];
        return new()
        {
            ModelCode = code,
            WmiCoolBoostSupported = predator ? null : false,
            WmiOperatingModesSupported = predator ? null : !noWmiModes,
            RequiresBalancedModeReset = predator,
            FanBoostReadbackReliable = code != "ANV16S-61",
            SupportsFnLockProbe = code is "ANV16-71" or "ANV16S-71",
            KeyboardControl = unsupported ? EcKeyboardControl.Unsupported
                : brightnessOnly ? EcKeyboardControl.BrightnessOnly
                : EcKeyboardControl.Rgb,
            // A stub on the 41, 72 and I31; elsewhere the EC never reads the mask it stores.
            KeyboardZoneSwitches = false,
            ResetsBlackStaticColors = code == "ANV16-41",
            BiosPasswordMaxLength = longPassword ? 128 : 16,
        };
    }
}
