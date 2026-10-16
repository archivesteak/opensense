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

    /// <summary>Which of the model's boards, where one BIOS package holds two that behave differently (the ANV15-51's ZYP and ZYQ).</summary>
    public string? Board { get; init; }

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

    /// <summary>
    /// A fan's Custom speed is its duty, not a boost over Auto: <c>SetGamingFanSpeed</c> writes 255 × percent / 100 to the
    /// fan's PWM register at once, and Custom stops the EC's own curve for that fan and writes the last speed set (PH717-71,
    /// ANV15-51 ZYP).
    /// </summary>
    public bool FixedFanSpeeds { get; init; }

    /// <summary>
    /// Where speeds are duties, the lowest one sent (a lower Custom speed goes out as this). The ANV15-51 ZYP's Custom
    /// behaviour writes the duty with no minimum, and nothing in its EC speeds a slow fan up; the BIOS's own speed path
    /// never goes below 23 % (CPU) and 20 % (GPU).
    /// </summary>
    public int MinimumFixedFanSpeed { get; init; }

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
    /// False where <c>GetGamingProfile</c> answers LCD overdrive as 0 but nothing handles it (the PH717-71's setter takes the
    /// Windows key and the lighting profile only).
    /// </summary>
    public bool? WmiLcdOverdriveSupported { get; init; }

    /// <summary>
    /// False where the Windows key lock the EC keeps reaches no keyboard: the PH717-71's EC applies it on its PS/2 path, and
    /// its keyboard is a USB one (whose own lock goes through the keyboard when it answers).
    /// </summary>
    public bool? WmiWindowsKeySupported { get; init; }

    /// <summary>
    /// False where the backlight auto-off function answers but nothing acts on what it keeps: the PH717-71's is a flag only
    /// its EC's own Fn-key handler flips and nothing reads (its keyboard, a USB one, has its own auto-off).
    /// </summary>
    public bool? WmiBacklightTimeoutSupported { get; init; }

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
        ["NL16-71G"] = "1.19", ["PH717-71"] = "1.10", ["ANV15-51"] = "1.53",
    };

    /// <param name="ecHid">
    /// The embedded controller's HID interface answered: it tells the ANV15-51's boards apart (ZYQ's EC has one, ZYP's
    /// hasn't; its BIOS reports gaming interface 2.94 there, 2.93 on ZYP).
    /// </param>
    public static AcerFirmwareProfile For(string? model, string? biosVersion = null, bool ecHid = false)
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
        // The Helios 700 has CoolBoost and no modes; its fan speeds are duties, its keyboard is a USB one, and overdrive, the
        // EC's Windows key lock and the auto-off function have nothing behind them.
        var helios700 = code == "PH717-71";
        // The ANV15-51's package holds two boards. ZYP: misc 0x0B sets the mode itself (Quiet, Balanced, Performance, Eco),
        // fan speeds are duties, and the keyboard methods are stubs (its EC changes the brightness with the Fn keys alone).
        // ZYQ: misc 0x0A/0x0B are refused and the EC HID interface sets the modes and the brightness; fan speeds are boosts.
        // Function 7 is a stub on both. Without an answering HID interface it is taken for ZYP: fans then go to full speed
        // near the limit instead of slowing there.
        var zyp = code == "ANV15-51" && !ecHid;
        var zyq = code == "ANV15-51" && ecHid;
        // Misc 0x0A/0x0B and the keyboard methods are stubs in SMM (refused on the PH717-71); the modes, where there are any,
        // go through the EC HID interface or function 7.
        var noWmiModes = lite || helios700 || code is "ANV16-72" or "ANV16-I31";
        // The record is stored but the EC reads none of it: the 71's keyboard colour follows the operating mode, the
        // KH62 boards' keyboard is the EC's own I2C one.
        var unsupported = zyp || noWmiModes || code is "ANV16-71" or "ANV16-42" or "ANV16-61";
        // The EC sends the lighting chip the brightness alone (the S71's the effect number too, without colours).
        var brightnessOnly = predator || code is "ANV16-A31" or "ANV16-A71" or "ANV16S-41" or "ANV16S-61" or "ANV16S-71";
        // The password check takes 128 characters here; the others take 16, or their setup does.
        var longPassword = code is "ANV16-72" or "ANV16-I31" or "ANV16S-71" or "PHN16-73" && revision == ReviewedBios[code];
        return new()
        {
            ModelCode = code,
            Board = zyp ? "ZYP" : zyq ? "ZYQ" : null,
            WmiCoolBoostSupported = predator || helios700 ? null : false,
            WmiOperatingModesSupported = predator ? null : !noWmiModes && !zyq,
            RequiresBalancedModeReset = predator,
            FanBoostReadbackReliable = code != "ANV16S-61",
            FixedFanSpeeds = helios700 || zyp,
            MinimumFixedFanSpeed = zyp ? 30 : 0,
            SupportsFnLockProbe = code is "ANV16-71" or "ANV16S-71",
            // ZYQ's brightness goes through the EC HID interface, which the probe finds; its WMI keyboard methods are stubs too.
            KeyboardControl = unsupported ? EcKeyboardControl.Unsupported
                : zyq ? EcKeyboardControl.Default
                : brightnessOnly ? EcKeyboardControl.BrightnessOnly
                : EcKeyboardControl.Rgb,
            // A stub on the 41, 72 and I31; elsewhere the EC never reads the mask it stores (the PH717-71's method sets
            // indicator lights).
            KeyboardZoneSwitches = false,
            ResetsBlackStaticColors = code == "ANV16-41",
            WmiLcdOverdriveSupported = helios700 ? false : null,
            WmiWindowsKeySupported = helios700 ? false : null,
            WmiBacklightTimeoutSupported = helios700 ? false : null,
            BiosPasswordMaxLength = longPassword ? 128 : 16,
        };
    }
}
