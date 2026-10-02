using System.Text.RegularExpressions;

namespace OpenSense.Core.Hardware;

/// <summary>
/// Built-in model defaults established from firmware and model configuration. Per-unit keyboard type, zones and
/// timeout functions remain unknown unless established separately; SMBIOS and live interfaces take precedence.
/// </summary>
public sealed record AcerModelProfile
{
    private static readonly IReadOnlyList<double> NeutralColorScale = Array.AsReadOnly(new[] { 1d, 1d, 1d });

    public string? ModelCode { get; init; }
    public bool? OperatingModes { get; init; }
    public int? CpuFanCount { get; init; }
    public int? GpuFanCount { get; init; }
    public int? SystemFanCount { get; init; }
    public bool? CpuFan => CpuFanCount is { } count ? count > 0 : null;
    public bool? GpuFan => GpuFanCount is { } count ? count > 0 : null;
    public bool? SystemFan => SystemFanCount is { } count ? count > 0 : null;
    public bool? RgbKeyboard { get; init; }
    public int? KeyboardZones { get; init; }
    public byte? KeyboardTimeoutHotkey { get; init; }
    public bool? WindowsKeyLock { get; init; }
    public bool? LcdOverdrive { get; init; }
    public KeyboardPayloadLayout? KeyboardLayout { get; init; }

    /// <summary>The effects the embedded controller's keyboard has, where they are fewer than <see cref="KeyboardProtocol.ZonedEffects"/>.</summary>
    public IReadOnlyList<KeyboardEffect>? KeyboardEffects { get; init; }
    public IReadOnlyList<double> KeyboardColorScale { get; init; } = NeutralColorScale;

    private static readonly AcerModelProfile Unknown = new();
    private static readonly Dictionary<string, AcerModelProfile> Models = CreateModels();

    public static AcerModelProfile For(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return Unknown;
        foreach (var (code, profile) in Models)
        {
            if (Regex.IsMatch(model, $"(?<![A-Za-z0-9]){Regex.Escape(code)}(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return profile with { ModelCode = code };
        }
        return Unknown;
    }

    private static Dictionary<string, AcerModelProfile> CreateModels()
    {
        var models = new Dictionary<string, AcerModelProfile>(StringComparer.OrdinalIgnoreCase);
        var older = new AcerModelProfile { OperatingModes = false, KeyboardLayout = KeyboardPayloadLayout.NitroSense };
        var dualFan = older with
        {
            CpuFanCount = 1, GpuFanCount = 1, SystemFanCount = 0,
            WindowsKeyLock = true, LcdOverdrive = true,
        };
        var modes = dualFan with { OperatingModes = true };

        Add(older, ["AN515-42", "AN515-52", "AN515-53"]);
        Add(dualFan,
        [
            "AN515-43", "AN515-44", "AN515-45", "AN515-51S", "AN515-54", "AN515-55", "AN515-56", "AN515-57",
            "AN517-41", "AN517-51", "AN517-52", "AN517-53", "AN517-54", "AN715-41", "AN715-51", "AN715-52",
            "N9500-A", "T9300",
        ]);
        Add(modes, ["AN515-46", "AN515-47", "AN515-58", "AN517-42", "AN517-43", "AN517-55", "ANX"]);
        // This fallback was measured on AN515-57; the caller still verifies the read before enabling it.
        models["AN515-57"] = dualFan with { KeyboardTimeoutHotkey = 0x84 };
        // Its EC firmware (V1.13) has no handler for effects 6 and 7.
        models["AN515-45"] = dualFan with { KeyboardEffects = KeyboardProtocol.FiveZonedEffects };
        models["PTX17-71"] = new()
        {
            CpuFanCount = 1, GpuFanCount = 2, SystemFanCount = 0,
        };
        return models;

        void Add(AcerModelProfile profile, IEnumerable<string> codes)
        {
            foreach (var code in codes)
                models.Add(code, profile);
        }
    }
}
