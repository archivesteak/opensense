using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.UI.Windowing;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Updates;

namespace OpenSense.App.Services;

/// <summary>
/// What the app does while its window is hidden in the notification area, for the diagnostics. Temporary: some users see it
/// use CPU there, and this is to find out what. The first <see cref="Settle"/> after each hide are counted apart (the window
/// finishing what it was doing); after that, the CPU by thread, the GPU time, .NET's counters and the app's own UI work
/// (<see cref="UiActivity"/>) add up, in all and by the page left open. A timeline keeps each of the last minutes' CPU.
/// </summary>
public sealed class TrayUsage : IDisposable
{
    private const string NeverShown = "never shown";
    private const int TimelineMinutes = 60;
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Usage _hidden = new();
    private readonly Dictionary<string, PageUsage> _pages = new(StringComparer.Ordinal);
    private TimeSpan _settleCpu, _settleWall;
    private int _hides, _shows;
    private Stretch? _stretch;
    private Timer? _settleTimer;
    private Func<string> _page = () => "?";

    private readonly Queue<(string State, double Percent)> _minutes = new();
    private Timer? _minuteTimer;
    private (DateTime At, TimeSpan Cpu) _minuteStart;
    private bool _minuteMixed;

    /// <summary>One time the window was hidden: the page it showed, when, and the process once it had settled.</summary>
    private sealed class Stretch(string page, DateTime at, TimeSpan cpu)
    {
        public string Page { get; } = page;

        public DateTime At { get; } = at;

        public TimeSpan Cpu { get; } = cpu;

        public Sample? Settled { get; set; }
    }

    private sealed record Sample(UsageSnapshot Usage, IReadOnlyDictionary<string, TimeSpan> Gpu, long[] Work);

    /// <summary>The time spent hidden on one page (after the first <see cref="Settle"/>): its CPU, GPU time and UI work.</summary>
    private sealed record PageUsage(TimeSpan Wall, TimeSpan Cpu, IReadOnlyDictionary<string, TimeSpan> Gpu, long[] Work)
    {
        public static readonly PageUsage None = new(TimeSpan.Zero, TimeSpan.Zero, new Dictionary<string, TimeSpan>(), UiActivity.Zero());

        public PageUsage Plus(Usage part, IReadOnlyDictionary<string, TimeSpan> gpu, long[] work) =>
            new(Wall + part.Wall, Cpu + part.Cpu, GpuTime.Plus(Gpu, gpu), UiActivity.Plus(Work, work));
    }

    /// <summary>
    /// Follows <paramref name="window"/> being shown and hidden; <paramref name="page"/> names the page it shows.
    /// <paramref name="startsHidden"/>: started in the notification area, at sign-in.
    /// </summary>
    public void Watch(AppWindow window, Func<string> page, bool startsHidden)
    {
        _page = page;
        _minuteStart = Now();
        _minuteTimer = new Timer(_ => Minute(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        if (startsHidden)
            Hide(NeverShown);
        window.Changed += (sender, args) =>
        {
            if (!args.DidVisibilityChange)
                return;
            if (sender.IsVisible)
                Show();
            else
                Hide(_page());
        };
    }

    private void Hide(string page)
    {
        try
        {
            var (at, cpu) = Now();
            lock (_gate)
            {
                if (_stretch is not null)
                    return;
                _hides++;
                _minuteMixed = true;
                var stretch = _stretch = new Stretch(page, at, cpu);
                _settleTimer?.Dispose();
                _settleTimer = new Timer(_ => Settled(stretch), null, Settle, Timeout.InfiniteTimeSpan);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Warning(ex, "Tray diagnostics failed");
        }
    }

    /// <summary><see cref="Settle"/> after hiding: from here on it counts as hidden.</summary>
    private void Settled(Stretch stretch)
    {
        try
        {
            var sample = new Sample(ProcessUsage.Take(), GpuTime.OfProcess(Environment.ProcessId), UiActivity.Snapshot());
            lock (_gate)
            {
                if (!ReferenceEquals(_stretch, stretch))
                    return;
                stretch.Settled = sample;
                _settleCpu += sample.Usage.Cpu - stretch.Cpu;
                _settleWall += sample.Usage.At - stretch.At;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Warning(ex, "Tray diagnostics failed");
        }
    }

    private void Show()
    {
        try
        {
            var work = UiActivity.Snapshot();
            var (at, cpu) = Now();
            Stretch? ended;
            lock (_gate)
            {
                ended = _stretch;
                if (ended is null)
                    return;
                _stretch = null;
                _shows++;
                _minuteMixed = true;
                _settleTimer?.Dispose();
                _settleTimer = null;
                if (ended.Settled is null)
                {
                    _settleCpu += cpu - ended.Cpu;
                    _settleWall += at - ended.At;
                    return;
                }
            }
            // The threads now, before the window draws; the GPU's counters and the sums off the UI thread.
            var usage = ProcessUsage.Take(ended.Settled.Usage);
            // Nothing navigates while hidden: the page now is the one a window that was never shown had loaded.
            var page = ended.Page == NeverShown ? $"{NeverShown}, {_page()} loaded" : ended.Page;
            ThreadPool.QueueUserWorkItem(_ => Ended(ended, page, usage, work));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Warning(ex, "Tray diagnostics failed");
        }
    }

    private void Ended(Stretch stretch, string page, UsageSnapshot usage, long[] work)
    {
        try
        {
            var settled = stretch.Settled!;
            var gpu = GpuTime.OfProcess(Environment.ProcessId);
            var part = Usage.Between(settled.Usage, usage);
            lock (_gate)
                Add(page, part, GpuTime.Since(settled.Gpu, gpu), UiActivity.Since(settled.Work, work), _hidden, _pages);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Warning(ex, "Tray diagnostics failed");
        }
    }

    private static void Add(string page, Usage part, IReadOnlyDictionary<string, TimeSpan> gpu, long[] work, Usage hidden,
        Dictionary<string, PageUsage> pages)
    {
        hidden.Add(part);
        pages[page] = pages.GetValueOrDefault(page, PageUsage.None).Plus(part, gpu, work);
    }

    private void Minute()
    {
        try
        {
            var (at, cpu) = Now();
            lock (_gate)
            {
                var state = _minuteMixed ? "mixed" : _stretch is { } stretch ? Hidden(stretch.Page) : "shown";
                _minutes.Enqueue((state, ProcessUsage.Percent(cpu - _minuteStart.Cpu, at - _minuteStart.At)));
                while (_minutes.Count > TimelineMinutes)
                    _minutes.Dequeue();
                _minuteStart = (at, cpu);
                _minuteMixed = false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Warning(ex, "Tray diagnostics failed");
        }
    }

    /// <summary>The app's part of the diagnostics.</summary>
    public string Describe()
    {
        try
        {
            return DescribeAll();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"App diagnostics failed: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
        }
    }

    private string DescribeAll()
    {
        var now = ProcessUsage.Take();
        var gpuNow = GpuTime.OfProcess(Environment.ProcessId);
        var workNow = UiActivity.Snapshot();
        var text = new StringBuilder();
        var kind = UpdateInstaller.DetectKind() == InstallKind.Installer ? "installed" : "portable";
        text.AppendLine(CultureInfo.InvariantCulture,
            $"App process ({Path.GetFileName(Environment.ProcessPath)}, {kind}{(SystemInfo.IsElevated ? ", elevated" : "")}), since it started:");
        var lifetime = Usage.Between(null, now);
        lifetime.Describe(text, "  ");
        text.AppendLine(CultureInfo.InvariantCulture, $"  GPU time: {GpuTime.Describe(gpuNow, lifetime.Wall)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  UI work: {UiActivity.Describe(workNow)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  now: {now.State}");

        lock (_gate)
        {
            // Copies, with the stretch going on now if the window is hidden while this is written.
            var hidden = new Usage();
            hidden.Add(_hidden);
            var pages = new Dictionary<string, PageUsage>(_pages, StringComparer.Ordinal);
            if (_stretch is { Settled: { } settled } stretch)
                Add(stretch.Page, Usage.Between(settled.Usage, now), GpuTime.Since(settled.Gpu, gpuNow), UiActivity.Since(settled.Work, workNow), hidden, pages);

            text.AppendLine(CultureInfo.InvariantCulture,
                $"Window: hidden {_hides} times, shown {_shows} times; the first {Settle.TotalSeconds:0} s after hiding: {ProcessUsage.Describe(_settleCpu, _settleWall)}");
            if (hidden.Wall > TimeSpan.Zero)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"While hidden, after those first {Settle.TotalSeconds:0} s:");
                hidden.Describe(text, "  ");
                foreach (var (page, use) in pages.OrderByDescending(p => p.Value.Wall))
                {
                    text.AppendLine(CultureInfo.InvariantCulture,
                        $"  {(page.StartsWith(NeverShown, StringComparison.Ordinal) ? page : $"on {page}")}, {ProcessUsage.Duration(use.Wall)}: {ProcessUsage.Percent(use.Cpu, use.Wall):0.00}% of one core");
                    text.AppendLine(CultureInfo.InvariantCulture, $"    GPU time: {GpuTime.Describe(use.Gpu, use.Wall)}");
                    text.AppendLine(CultureInfo.InvariantCulture, $"    UI work: {UiActivity.Describe(use.Work)}");
                }
            }
            else
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"While hidden: not hidden for longer than {Settle.TotalSeconds:0} s yet");
            }
            text.AppendLine(CultureInfo.InvariantCulture, $"CPU per minute, last {_minutes.Count} min, % of one core: {Timeline()}");
        }
        text.AppendLine(CultureInfo.InvariantCulture, $"Diagnostics' own snapshots: {ProcessUsage.OwnCost}");
        return text.ToString();
    }

    /// <summary>The minutes, the same state together: "hidden on Fans: 0.4 0.5 | shown: 12.1 | …".</summary>
    private string Timeline()
    {
        var groups = new List<(string State, List<double> Values)>();
        foreach (var (state, percent) in _minutes)
        {
            if (groups.Count > 0 && groups[^1].State == state)
                groups[^1].Values.Add(percent);
            else
                groups.Add((state, [percent]));
        }
        return groups.Count == 0 ? "none yet"
            : string.Join(" | ", groups.Select(g => $"{g.State}: {string.Join(" ", g.Values.Select(v => v.ToString("0.0", CultureInfo.InvariantCulture)))}"));
    }

    private static string Hidden(string page) => page == NeverShown ? "hidden, never shown" : $"hidden on {page}";

    private static (DateTime At, TimeSpan Cpu) Now()
    {
        using var process = Process.GetCurrentProcess();
        return (DateTime.UtcNow, process.TotalProcessorTime);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _settleTimer?.Dispose();
            _settleTimer = null;
            _minuteTimer?.Dispose();
            _minuteTimer = null;
        }
    }
}
