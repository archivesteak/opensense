using System.Text.Json;
using OpenSense.Core.Lighting;
using Windows.Foundation;

namespace OpenSense.App.Controls;

/// <summary>A key as the per-key editor draws it: its code, its legend and where it is (in key units; the Enter of ISO and JIS takes two rectangles).</summary>
public sealed record LayoutKey(string Code, string Legend, IReadOnlyList<Rect> Rects)
{
    public Rect Bounds => Rects.Skip(1).Aggregate(Rects[0], (a, b) =>
    {
        a.Union(b);
        return a;
    });
}

/// <summary>A laptop keyboard's keys (<c>Assets/Layouts/*.json</c>).</summary>
public sealed record KeyLayout(KeyboardLayout Kind, double Width, double Height, IReadOnlyList<LayoutKey> Keys);

/// <summary>The per-key editor's layouts, read once from the app's folder.</summary>
public static class KeyboardLayouts
{
    private static readonly Dictionary<KeyboardLayout, KeyLayout> Cache = [];
    private static readonly Lazy<HashSet<string>> Codes = new(() =>
        [.. Enum.GetValues<KeyboardLayout>().SelectMany(l => Get(l).Keys.Select(k => k.Code))]);

    public static KeyLayout Get(KeyboardLayout kind)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(kind, out var layout))
                Cache[kind] = layout = Load(kind);
            return layout;
        }
    }

    /// <summary>Whether a key sits on any of the layouts (keys that sit on none, like Power, are drawn in a row of their own).</summary>
    public static bool OnAnyLayout(string code) => Codes.Value.Contains(code);

    private static KeyLayout Load(KeyboardLayout kind)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Layouts", kind.ToString().ToLowerInvariant() + ".json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        List<LayoutKey> keys = [];
        foreach (var key in root.GetProperty("keys").EnumerateArray())
        {
            List<Rect> rects = [];
            foreach (var rect in key.GetProperty("rects").EnumerateArray())
                rects.Add(new Rect(rect[0].GetDouble(), rect[1].GetDouble(), rect[2].GetDouble(), rect[3].GetDouble()));
            keys.Add(new LayoutKey(key.GetProperty("code").GetString()!, key.GetProperty("legend").GetString() ?? "", rects));
        }
        return new KeyLayout(kind, root.GetProperty("width").GetDouble(), root.GetProperty("height").GetDouble(), keys);
    }
}
