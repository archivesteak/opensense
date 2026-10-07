using System.Diagnostics;
using System.Globalization;

namespace OpenSense.App.Services;

/// <summary>The app's own drawing and updates that <see cref="UiActivity"/> counts.</summary>
public enum UiWork
{
    /// <summary>Readings from the service applied to the view models (with the bindings and tray tooltip they update).</summary>
    TelemetryUpdates,

    /// <summary>XAML layout passes in the main window.</summary>
    LayoutPasses,

    /// <summary>Win2D canvases drawn: charts, fans, curve editor, keyboard and light previews.</summary>
    CanvasDraws,

    /// <summary>Frames the keyboard preview ran its effect for.</summary>
    PreviewFrames,

    /// <summary>Spin animations started by the fan rotors.</summary>
    FanAnimations,

    /// <summary>Frames of the fan rotors' boost-ring animation.</summary>
    BoostFrames,

    /// <summary>Notification-area tooltips changed.</summary>
    TooltipUpdates,

    /// <summary>Rebuilds, reconnections and settings changed elsewhere: each makes every view model attach again.</summary>
    SessionChanges,
}

/// <summary>
/// Counts of the app's own drawing and updates, for the diagnostics. Temporary: they show whether a window hidden in the
/// notification area still does any of them (see <see cref="TrayUsage"/>).
/// </summary>
public static class UiActivity
{
    // One count per UiWork, then the telemetry updates' total time in Stopwatch ticks.
    private static readonly long[] Counts = new long[Enum.GetValues<UiWork>().Length + 1];

    public static void Count(UiWork work) => Interlocked.Increment(ref Counts[(int)work]);

    /// <summary>A telemetry update that began at <paramref name="started"/> (a <see cref="Stopwatch"/> timestamp) is done.</summary>
    public static void CountTelemetry(long started)
    {
        Interlocked.Increment(ref Counts[(int)UiWork.TelemetryUpdates]);
        Interlocked.Add(ref Counts[^1], Stopwatch.GetTimestamp() - started);
    }

    public static long[] Snapshot()
    {
        var copy = new long[Counts.Length];
        for (var i = 0; i < copy.Length; i++)
            copy[i] = Interlocked.Read(ref Counts[i]);
        return copy;
    }

    public static long[] Since(long[] earlier, long[] now) => [.. now.Select((count, i) => count - earlier[i])];

    public static long[] Plus(long[] a, long[] b) => [.. a.Select((count, i) => count + b[i])];

    public static long[] Zero() => new long[Counts.Length];

    /// <summary>E.g. "3000 telemetry updates (0.80 ms each), 3100 layout passes, 0 Win2D draws, …".</summary>
    public static string Describe(long[] counts)
    {
        var updates = counts[(int)UiWork.TelemetryUpdates];
        var each = updates == 0 ? 0 : counts[^1] * 1000.0 / Stopwatch.Frequency / updates;
        return string.Create(CultureInfo.InvariantCulture,
            $"{updates} telemetry updates ({each:0.00} ms each), {counts[(int)UiWork.SessionChanges]} session changes, {counts[(int)UiWork.LayoutPasses]} layout passes, " +
            $"{counts[(int)UiWork.CanvasDraws]} Win2D draws, {counts[(int)UiWork.PreviewFrames]} keyboard preview frames, " +
            $"{counts[(int)UiWork.FanAnimations]} fan spin animations, {counts[(int)UiWork.BoostFrames]} fan boost frames, " +
            $"{counts[(int)UiWork.TooltipUpdates]} tooltip updates");
    }
}
