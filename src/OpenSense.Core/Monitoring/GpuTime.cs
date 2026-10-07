using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// The GPU time a process has used so far, by GPU and engine, from Windows' "GPU Engine" counters (a running time per process
/// and engine). For the diagnostics, temporary: whether a hidden window still draws, and on which GPU.
/// </summary>
public static partial class GpuTime
{
    /// <summary>The process's GPU time by "GPU engine" (e.g. "NVIDIA GeForce RTX 3070 Laptop GPU 3D"); empty where unknown.</summary>
    public static IReadOnlyDictionary<string, TimeSpan> OfProcess(int processId)
    {
        var result = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        try
        {
            using var query = new PdhQuery();
            if (query.Add(@"\GPU Engine(*)\Running Time") is not { } counter || !query.Collect())
                return result;
            var prefix = string.Create(CultureInfo.InvariantCulture, $"pid_{processId}_");
            var adapters = GpuAdapters.TryEnumerate();
            foreach (var (instance, value) in PdhQuery.RawValues(counter))
            {
                if (!instance.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var luid = LuidPattern().Match(instance).Value;
                // Not a GPU that DXGI lists, e.g. a virtual display's adapter.
                var gpu = adapters.FirstOrDefault(a => a.CounterLuid.Equals(luid, StringComparison.OrdinalIgnoreCase))?.Name ?? $"other adapter {luid}";
                var at = instance.LastIndexOf("engtype_", StringComparison.Ordinal);
                var key = $"{gpu} {(at < 0 ? "engine" : instance[(at + "engtype_".Length)..])}";
                result[key] = result.GetValueOrDefault(key) + TimeSpan.FromTicks(value); // 100 ns units
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result.Clear();
        }
        return result;
    }

    /// <summary>What <paramref name="now"/> added to <paramref name="earlier"/>.</summary>
    public static Dictionary<string, TimeSpan> Since(IReadOnlyDictionary<string, TimeSpan> earlier, IReadOnlyDictionary<string, TimeSpan> now) =>
        now.ToDictionary(e => e.Key, e => e.Value - earlier.GetValueOrDefault(e.Key), StringComparer.Ordinal);

    public static Dictionary<string, TimeSpan> Plus(IReadOnlyDictionary<string, TimeSpan> a, IReadOnlyDictionary<string, TimeSpan> b)
    {
        var sum = new Dictionary<string, TimeSpan>(a, StringComparer.Ordinal);
        foreach (var (engine, time) in b)
            sum[engine] = sum.GetValueOrDefault(engine) + time;
        return sum;
    }

    /// <summary>E.g. "Intel(R) UHD Graphics 3D 12.1 s (0.40% of the time)", the busiest engines first.</summary>
    public static string Describe(IReadOnlyDictionary<string, TimeSpan> times, TimeSpan wall)
    {
        var used = times.Where(t => t.Value >= TimeSpan.FromMilliseconds(1)).OrderByDescending(t => t.Value).ToList();
        return used.Count == 0 ? "none"
            : string.Join(" | ", used.Select(t => string.Create(CultureInfo.InvariantCulture,
                $"{t.Key} {t.Value.TotalSeconds:0.0} s ({ProcessUsage.Percent(t.Value, wall):0.00}% of the time)")));
    }

    [GeneratedRegex("luid_0x[0-9A-Fa-f]{8}_0x[0-9A-Fa-f]{8}")]
    private static partial Regex LuidPattern();
}
