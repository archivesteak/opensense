using Windows.Win32;
using Windows.Win32.System.Performance;

namespace OpenSense.Core.Monitoring;

/// <summary>One Performance Data Helper query holding any number of counters.</summary>
internal sealed class PdhQuery : IDisposable
{
    private const PDH_FMT Format = PDH_FMT.PDH_FMT_DOUBLE; // values are capped at 100 %, which is what we display

    private readonly PdhCloseQuerySafeHandle? _query;

    public PdhQuery() =>
        _query = PInvoke.PdhOpenQuery(null, 0, out var query) == 0 ? query : null;

    public PDH_HCOUNTER? Add(string englishPath) =>
        _query is not null && PInvoke.PdhAddEnglishCounter(_query, englishPath, 0, out var counter) == 0 ? counter : null;

    public bool Collect() => _query is not null && PInvoke.PdhCollectQueryData((PDH_HQUERY)_query.DangerousGetHandle()) == 0;

    public static double? Value(PDH_HCOUNTER counter) =>
        PInvoke.PdhGetFormattedCounterValue(counter, Format, out var value) == 0 && IsValid(value.CStatus)
            ? value.doubleValue
            : null;

    /// <summary>All instances of a wildcard counter, e.g. <c>\GPU Engine(*)\Utilization Percentage</c>.</summary>
    public static unsafe List<(string Instance, double Value)> Values(PDH_HCOUNTER counter)
    {
        var result = new List<(string, double)>();
        uint size = 0, count = 0;
        if (PInvoke.PdhGetFormattedCounterArray(counter, Format, &size, &count, null) != PInvoke.PDH_MORE_DATA || size == 0)
            return result;

        var buffer = new byte[size];
        fixed (byte* raw = buffer)
        {
            var items = (PDH_FMT_COUNTERVALUE_ITEM_W*)raw;
            if (PInvoke.PdhGetFormattedCounterArray(counter, Format, &size, &count, items) != 0)
                return result;
            for (var i = 0; i < count; i++)
            {
                if (IsValid(items[i].FmtValue.CStatus))
                    result.Add((items[i].szName.ToString(), items[i].FmtValue.doubleValue));
            }
        }
        return result;
    }

    // PDH_CSTATUS_VALID_DATA (0) or PDH_CSTATUS_NEW_DATA (1).
    private static bool IsValid(uint status) => status <= 1;

    public void Dispose() => _query?.Dispose();
}
