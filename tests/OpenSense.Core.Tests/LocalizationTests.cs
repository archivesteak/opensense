using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenSense.Core.Tests;

/// <summary>
/// The translations against the English text and the code that uses it: the app's Strings\&lt;language&gt;\Resources.resw,
/// the languages Settings offers, and the installer's Strings\&lt;language&gt;.nsh. Reads the source tree.
/// </summary>
public sealed partial class LocalizationTests
{
    private const string English = "en-US";
    private const string InstallerEnglish = "English";

    private static readonly string Root = SourceTree.Root;
    private static readonly string AppFolder = Path.Combine(Root, "src", "OpenSense.App");
    private static readonly string StringsFolder = Path.Combine(AppFolder, "Strings");
    private static readonly string InstallerFolder = Path.Combine(Root, "installer");
    private static readonly string InstallerStringsFolder = Path.Combine(InstallerFolder, "Strings");

    public static TheoryData<string> Translations => new(AppLanguages().Where(l => l != English));

    public static TheoryData<string> InstallerTranslations => new(InstallerLanguages().Where(l => l != InstallerEnglish));

    [Theory, MemberData(nameof(Translations))]
    public void Translation_has_the_English_strings(string language)
    {
        var english = Resources(English);
        var translated = Resources(language);
        var problems = new List<string>();
        problems.AddRange(english.Keys.Except(translated.Keys).Select(k => $"missing {k}"));
        problems.AddRange(translated.Keys.Except(english.Keys).Select(k => $"unknown {k}"));
        foreach (var (key, source) in english)
        {
            if (!translated.TryGetValue(key, out var text))
                continue;
            if (string.IsNullOrWhiteSpace(text))
                problems.Add($"{key} is empty");
            if (!Placeholders(text).SetEquals(Placeholders(source)))
                problems.Add($"{key} has placeholders {string.Join(' ', Placeholders(text))}, English {string.Join(' ', Placeholders(source))}");
            if (text.Count('\n') != source.Count('\n'))
                problems.Add($"{key} has {text.Count('\n')} line breaks, English {source.Count('\n')}");
        }
        Assert.Empty(problems);
    }

    [Fact]
    public void Settings_offers_every_translation()
    {
        var source = File.ReadAllText(Path.Combine(AppFolder, "Localization", "AppLanguage.cs"));
        var offered = LanguageOptionRegex().Matches(source).Select(m => m.Groups[1].Value);
        Assert.Equal(AppLanguages().Order(StringComparer.Ordinal), offered.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Strings_the_app_asks_for_exist()
    {
        var keys = Resources(English).Keys.ToHashSet(StringComparer.Ordinal);
        var appLanguage = File.ReadAllText(Path.Combine(AppFolder, "Localization", "AppLanguage.cs"));
        var probe = ProbeResourceRegex().Match(appLanguage).Groups[1].Value.Replace('/', '.');

        var missing = KeysInCode().Append(probe).Where(k => !keys.Contains(k))
            .Concat(Uids().Where(uid => !keys.Any(k => k.StartsWith(uid + ".", StringComparison.Ordinal))).Select(uid => "x:Uid " + uid));
        Assert.Empty(missing.Distinct());
    }

    [Fact]
    public void Every_string_is_used()
    {
        var used = KeysInCode().ToHashSet(StringComparer.Ordinal);
        var uids = Uids().ToHashSet(StringComparer.Ordinal);
        var unused = Resources(English).Keys.Where(k => !used.Contains(k) && !(k.IndexOf('.', StringComparison.Ordinal) is > 0 and var dot && uids.Contains(k[..dot])));
        Assert.Empty(unused);
    }

    [Fact]
    public void Installer_has_every_language()
    {
        var script = File.ReadAllText(Path.Combine(InstallerFolder, "OpenSense.nsi"));
        var loaded = InstallerLanguageRegex().Matches(script).Select(m => m.Groups[1].Value);
        Assert.Equal(InstallerLanguages().Order(StringComparer.Ordinal), loaded.Order(StringComparer.Ordinal));
        Assert.Equal(AppLanguages().Count(), InstallerLanguages().Count());
    }

    [Theory, MemberData(nameof(InstallerTranslations))]
    public void Installer_translation_has_the_English_strings(string language)
    {
        var english = InstallerStrings(InstallerEnglish, out _);
        var translated = InstallerStrings(language, out var declared);
        var problems = new List<string>();
        if (declared != language)
            problems.Add($"LANGFILE_EXT names {declared}");
        problems.AddRange(english.Keys.Except(translated.Keys).Select(k => $"missing {k}"));
        problems.AddRange(translated.Keys.Except(english.Keys).Select(k => $"unknown {k}"));
        foreach (var (key, source) in english)
        {
            if (translated.TryGetValue(key, out var text) && !InstallerVariables(text).SetEquals(InstallerVariables(source)))
                problems.Add($"{key} uses {string.Join(' ', InstallerVariables(text))}, English {string.Join(' ', InstallerVariables(source))}");
        }
        Assert.Empty(problems);
    }

    private static IEnumerable<string> AppLanguages() => Directory.GetDirectories(StringsFolder).Select(Path.GetFileName)!;

    private static IEnumerable<string> InstallerLanguages() =>
        Directory.GetFiles(InstallerStringsFolder, "*.nsh").Select(Path.GetFileNameWithoutExtension)!;

    private static Dictionary<string, string> Resources(string language) =>
        XDocument.Load(Path.Combine(StringsFolder, language, "Resources.resw")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "", StringComparer.Ordinal);

    private static Dictionary<string, string> InstallerStrings(string language, out string? declared)
    {
        var text = File.ReadAllText(Path.Combine(InstallerStringsFolder, language + ".nsh"));
        declared = LangFileExtRegex().Match(text) is { Success: true } m ? m.Groups[1].Value : null;
        return LangFileStringRegex().Matches(text).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
    }

    private static HashSet<string> Placeholders(string text) => PlaceholderRegex().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    private static HashSet<string> InstallerVariables(string text) => InstallerVariableRegex().Matches(text).Select(m => m.Value).ToHashSet();

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppFolder, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Uids() =>
        SourceFiles("*.xaml").SelectMany(f => UidRegex().Matches(File.ReadAllText(f))).Select(m => m.Groups[1].Value);

    /// <summary>The keys passed to Strings.Get and Strings.Format: every string literal in their first argument.</summary>
    private static IEnumerable<string> KeysInCode()
    {
        foreach (var file in SourceFiles("*.cs"))
        {
            var code = File.ReadAllText(file);
            foreach (Match call in LookupRegex().Matches(code))
            {
                foreach (Match literal in LiteralRegex().Matches(FirstArgument(code, call.Index + call.Length)))
                    yield return literal.Groups[1].Value.Replace('/', '.');
            }
        }
    }

    private static string FirstArgument(string code, int start)
    {
        var depth = 0;
        for (var i = start; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '(':
                    depth++;
                    break;
                case ')' or ',' when depth == 0:
                    return code[start..i];
                case ')':
                    depth--;
                    break;
            }
        }
        return code[start..];
    }

    [GeneratedRegex(@"\bStrings\.(?:Get|Format)\(")]
    private static partial Regex LookupRegex();

    [GeneratedRegex("\"([^\"\\\\]*)\"")]
    private static partial Regex LiteralRegex();

    [GeneratedRegex("x:Uid=\"([^\"]+)\"")]
    private static partial Regex UidRegex();

    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex("new\\(\"([A-Za-z-]+)\", \"")]
    private static partial Regex LanguageOptionRegex();

    [GeneratedRegex("ProbeResource = \"Resources/([^\"]+)\"")]
    private static partial Regex ProbeResourceRegex();

    [GeneratedRegex("^!insertmacro OpenSenseLanguage \"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex InstallerLanguageRegex();

    [GeneratedRegex("^!insertmacro LANGFILE_EXT \"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex LangFileExtRegex();

    [GeneratedRegex("^\\$\\{LangFileString\\} (\\w+) \"(.*)\"\\r?$", RegexOptions.Multiline)]
    private static partial Regex LangFileStringRegex();

    [GeneratedRegex(@"\$(?:\d|\{\w+\})")]
    private static partial Regex InstallerVariableRegex();
}
