using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.System.UserProfile;

namespace OpenSense.App.Localization;

/// <summary>A language OpenSense is translated into: its folder under Strings, and its name in that language.</summary>
public sealed record LanguageOption(string Tag, string NativeName);

/// <summary>
/// The language OpenSense runs in: the one chosen in Settings, or the best match for the user's Windows languages.
/// Applied once at startup, before anything loads a string; a new choice takes effect when OpenSense restarts.
/// </summary>
public static class AppLanguage
{
    /// <summary>The language OpenSense is written in, used when no translation matches.</summary>
    public const string Default = "en-US";

    /// <summary>Any resource: every translation has them all, so the one the resource system picks names the language.</summary>
    private const string ProbeResource = "Resources/Nav_Dashboard/Content";

    /// <summary>One entry per Strings folder, named as Windows lists them, sorted for the Settings page.</summary>
    public static IReadOnlyList<LanguageOption> Available { get; } = new LanguageOption[]
    {
        new("ar-SA", "العربية"),
        new("bg-BG", "Български"),
        new("ca-ES", "Català"),
        new("cs-CZ", "Čeština"),
        new("de-DE", "Deutsch"),
        new("el-GR", "Ελληνικά"),
        new("en-US", "English"),
        new("es-ES", "Español"),
        new("et-EE", "Eesti"),
        new("fa-IR", "فارسی"),
        new("fi-FI", "Suomi"),
        new("fr-FR", "Français"),
        new("he-IL", "עברית"),
        new("hi-IN", "हिन्दी"),
        new("hu-HU", "Magyar"),
        new("id-ID", "Bahasa Indonesia"),
        new("it-IT", "Italiano"),
        new("ja-JP", "日本語"),
        new("ko-KR", "한국어"),
        new("lv-LV", "Latviešu"),
        new("nb-NO", "Norsk bokmål"),
        new("nl-NL", "Nederlands"),
        new("pl-PL", "Polski"),
        new("pt-BR", "Português (Brasil)"),
        new("pt-PT", "Português (Portugal)"),
        new("ro-RO", "Română"),
        new("ru-RU", "Русский"),
        new("sk-SK", "Slovenčina"),
        new("sr-Cyrl-RS", "Српски"),
        new("sv-SE", "Svenska"),
        new("th-TH", "ไทย"),
        new("tr-TR", "Türkçe"),
        new("uk-UA", "Українська"),
        new("vi-VN", "Tiếng Việt"),
        new("zh-CN", "中文(简体)"),
        new("zh-TW", "中文(繁體)"),
    }.OrderBy(l => l.NativeName, StringComparer.InvariantCulture).ToArray();

    /// <summary>The language in use, one of <see cref="Available"/>.</summary>
    public static string Current { get; private set; } = Default;

    /// <summary>Arabic, Hebrew and Persian lay the window out from right to left.</summary>
    public static bool IsRightToLeft => CultureInfo.GetCultureInfo(Current).TextInfo.IsRightToLeft;

    /// <summary>The language <paramref name="choice"/> (a tag, or null to follow Windows) comes down to.</summary>
    public static string Resolve(string? choice) => Find(choice) ?? BestMatch() ?? Default;

    /// <param name="choice">The language chosen in Settings, or null to follow Windows.</param>
    public static void Apply(string? choice)
    {
        Current = Resolve(choice);

        // x:Uid, ResourceLoader and WinUI's own .pri strings (such as NavigationView's Settings item).
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Current;
        // WinUI's control strings (On/Off, the colour picker) come from .mui files, by the process's preferred UI languages.
        SetPreferredUILanguages(Current == Default ? [Default] : [Current, Default]);
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(Current);
    }

    private static string? Find(string? tag) =>
        Available.FirstOrDefault(l => string.Equals(l.Tag, tag, StringComparison.OrdinalIgnoreCase))?.Tag;

    /// <summary>
    /// Matches the user's Windows languages against the translations the way the resource system matches them for a
    /// packaged app (regional variants, scripts, fallbacks down the list): asks it to pick a string and reads which
    /// translation that came from.
    /// </summary>
    private static string? BestMatch()
    {
        var resources = new ResourceManager();
        var context = resources.CreateResourceContext();
        context.QualifierValues[KnownResourceQualifierName.Language] = string.Join(';', GlobalizationPreferences.Languages);
        var candidate = resources.MainResourceMap.GetValue(ProbeResource, context);
        return candidate.QualifierValues.TryGetValue(KnownResourceQualifierName.Language, out var language) ? Find(language) : null;
    }

    private static unsafe void SetPreferredUILanguages(string[] languages)
    {
        fixed (char* list = string.Join('\0', languages) + "\0\0")
        {
            Windows.Win32.PInvoke.SetProcessPreferredUILanguages(Windows.Win32.PInvoke.MUI_LANGUAGE_NAME, list, null);
        }
    }
}
