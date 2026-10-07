using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Services;
using Windows.Win32.System.Threading;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// Where this process's CPU time goes, for the diagnostics. Temporary: some users see OpenSense use CPU in the background,
/// and this is to find out what. <see cref="Take"/> notes the process's CPU time, each thread's (named by
/// <see cref="NameThisThread"/>, by the name Windows or .NET gave it, or by the module it started in) and .NET's own
/// counters; <see cref="Usage.Between"/> turns two of these into what happened in between.
/// </summary>
public static class ProcessUsage
{
    private static readonly ConcurrentDictionary<uint, string> Names = new();
    private static long _snapshots, _snapshotTicks;

    /// <summary>When this process started (UTC).</summary>
    public static DateTime Started { get; } = StartTime();

    /// <summary>Names the calling thread in the diagnostics, e.g. "UI thread".</summary>
    public static void NameThisThread(string name) => Names[PInvoke.GetCurrentThreadId()] = name;

    /// <summary>The calling thread is ending: Windows may give its id to a new thread.</summary>
    public static void ForgetThisThread() => Names.TryRemove(PInvoke.GetCurrentThreadId(), out _);

    /// <summary>
    /// The process now. Threads found in <paramref name="known"/> keep the names found then, which saves looking at the
    /// process's modules again.
    /// </summary>
    public static UsageSnapshot Take(UsageSnapshot? known = null)
    {
        var started = Stopwatch.GetTimestamp();
        using var process = Process.GetCurrentProcess();
        List<(nint Start, nint End, string Name)>? modules = null;
        var threads = new Dictionary<(int Id, DateTime Started), (TimeSpan Cpu, string Name)>();
        foreach (ProcessThread thread in process.Threads)
        {
            using (thread)
            {
                try
                {
                    var key = (thread.Id, thread.StartTime);
                    var name = known is not null && known.Threads.TryGetValue(key, out var seen)
                        ? seen.Name
                        : ThreadName((uint)thread.Id, modules ??= Modules(process));
                    threads[key] = (thread.TotalProcessorTime, name);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // It ended meanwhile; its time stays in the process's total.
                }
            }
        }
        var snapshot = new UsageSnapshot(DateTime.UtcNow, process.TotalProcessorTime, threads, RuntimeCounters.Now(),
            process.HandleCount, Timer.ActiveCount, process.WorkingSet64, process.PrivateMemorySize64);
        Interlocked.Increment(ref _snapshots);
        Interlocked.Add(ref _snapshotTicks, Stopwatch.GetTimestamp() - started);
        return snapshot;
    }

    /// <summary>What the diagnostics' own snapshots have cost so far.</summary>
    public static string OwnCost => string.Create(CultureInfo.InvariantCulture,
        $"{Interlocked.Read(ref _snapshots)} snapshots, {Interlocked.Read(ref _snapshotTicks) * 1000.0 / Stopwatch.Frequency:0} ms");

    /// <summary>The process hosting a Windows service, by id and start time, and its CPU time so far; empty where it can't be read.</summary>
    public static Dictionary<(int Id, DateTime Started), TimeSpan> ServiceProcess(string service)
    {
        try
        {
            using var manager = PInvoke.OpenSCManager(null, null, PInvoke.SC_MANAGER_CONNECT);
            if (manager.IsInvalid)
                return [];
            using var handle = PInvoke.OpenService(manager, service, PInvoke.SERVICE_QUERY_STATUS);
            if (handle.IsInvalid)
                return [];
            var buffer = new byte[Marshal.SizeOf<SERVICE_STATUS_PROCESS>()];
            if (!PInvoke.QueryServiceStatusEx(handle, SC_STATUS_TYPE.SC_STATUS_PROCESS_INFO, buffer, out _))
                return [];
            var id = (int)MemoryMarshal.Read<SERVICE_STATUS_PROCESS>(buffer).dwProcessId;
            if (id == 0)
                return [];
            using var process = Process.GetProcessById(id);
            return new() { [(id, process.StartTime.ToUniversalTime())] = process.TotalProcessorTime };
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>Every process of this name (e.g. "WmiPrvSE") that can be read, by id and start time, and its CPU time so far.</summary>
    public static Dictionary<(int Id, DateTime Started), TimeSpan> ProcessesNamed(string name)
    {
        var result = new Dictionary<(int Id, DateTime Started), TimeSpan>();
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    result[(process.Id, process.StartTime.ToUniversalTime())] = process.TotalProcessorTime;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Ended meanwhile, or not readable with these rights.
                }
            }
        }
        return result;
    }

    /// <summary>
    /// What the processes in <paramref name="now"/> used since <paramref name="then"/> was taken at <paramref name="at"/>, all of
    /// it for one started since; how many are still running; and how many of <paramref name="then"/> ended (their time is lost).
    /// </summary>
    public static (TimeSpan Cpu, int Running, int Ended) Since(Dictionary<(int Id, DateTime Started), TimeSpan> then, DateTime at,
        Dictionary<(int Id, DateTime Started), TimeSpan> now)
    {
        var cpu = TimeSpan.Zero;
        foreach (var (key, time) in now)
        {
            if (then.TryGetValue(key, out var before))
                cpu += time - before;
            else if (key.Started >= at)
                cpu += time;
        }
        return (cpu, now.Count, then.Keys.Count(key => !now.ContainsKey(key)));
    }

    /// <summary>E.g. "83.2 s of CPU in 21.0 h: 0.11% of one core, 0.009% of all 12".</summary>
    public static string Describe(TimeSpan cpu, TimeSpan wall)
    {
        var oneCore = Percent(cpu, wall);
        return string.Create(CultureInfo.InvariantCulture,
            $"{cpu.TotalSeconds:0.0} s of CPU in {Duration(wall)}: {oneCore:0.00}% of one core, {oneCore / Environment.ProcessorCount:0.000}% of all {Environment.ProcessorCount}");
    }

    public static double Percent(TimeSpan part, TimeSpan wall) => wall > TimeSpan.Zero ? part / wall * 100 : 0;

    public static string Duration(TimeSpan time) =>
        time.TotalMinutes < 2 ? string.Create(CultureInfo.InvariantCulture, $"{time.TotalSeconds:0} s")
        : time.TotalHours < 2 ? string.Create(CultureInfo.InvariantCulture, $"{time.TotalMinutes:0} min")
        : string.Create(CultureInfo.InvariantCulture, $"{time.TotalHours:0.0} h");

    private static unsafe string ThreadName(uint id, List<(nint Start, nint End, string Name)> modules)
    {
        if (Names.TryGetValue(id, out var named))
            return named;
        using var handle = PInvoke.OpenThread_SafeHandle(THREAD_ACCESS_RIGHTS.THREAD_QUERY_INFORMATION, false, id);
        if (handle.IsInvalid)
            return "unknown";
        // Named by .NET (".NET TP Worker", ".NET Finalizer"…) or by whoever made it.
        if (PInvoke.GetThreadDescription(handle, out var description).Succeeded && description.Value is not null)
        {
            try
            {
                var text = description.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            finally
            {
                PInvoke.LocalFree((HLOCAL)description.Value);
            }
        }
        // Otherwise the module its code started in: the WinUI, DirectComposition or GPU-driver DLL, Windows' thread pool (ntdll)…
        nint start = 0;
        var status = Windows.Wdk.PInvoke.NtQueryInformationThread(new HANDLE(handle.DangerousGetHandle()),
            Windows.Wdk.System.Threading.THREADINFOCLASS.ThreadQuerySetWin32StartAddress, &start, (uint)sizeof(nint), null);
        if (status >= 0 && start != 0)
        {
            foreach (var (low, high, name) in modules)
            {
                if (start >= low && start < high)
                    return name;
            }
        }
        return "unknown";
    }

    private static List<(nint Start, nint End, string Name)> Modules(Process process)
    {
        var modules = new List<(nint, nint, string)>();
        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                using (module)
                    modules.Add((module.BaseAddress, module.BaseAddress + module.ModuleMemorySize, module.ModuleName));
            }
        }
        catch (Win32Exception)
        {
            // Threads without a name then stay "unknown".
        }
        return modules;
    }

    private static DateTime StartTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }
}

/// <summary>.NET's running totals: memory allocated, garbage collections and their pauses, thread-pool work, lock waits.</summary>
public readonly record struct RuntimeCounters(long Allocated, long Gen0, long Gen1, long Gen2, TimeSpan GcPause, long ThreadPoolItems, long LockWaits)
{
    public static RuntimeCounters Now() => new(GC.GetTotalAllocatedBytes(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
        GC.GetTotalPauseDuration(), ThreadPool.CompletedWorkItemCount, Monitor.LockContentionCount);

    /// <summary>What happened since <paramref name="earlier"/>.</summary>
    public RuntimeCounters Since(RuntimeCounters earlier) => new(Allocated - earlier.Allocated, Gen0 - earlier.Gen0, Gen1 - earlier.Gen1,
        Gen2 - earlier.Gen2, GcPause - earlier.GcPause, ThreadPoolItems - earlier.ThreadPoolItems, LockWaits - earlier.LockWaits);

    public RuntimeCounters Plus(RuntimeCounters other) => new(Allocated + other.Allocated, Gen0 + other.Gen0, Gen1 + other.Gen1,
        Gen2 + other.Gen2, GcPause + other.GcPause, ThreadPoolItems + other.ThreadPoolItems, LockWaits + other.LockWaits);
}

/// <summary>The process at one moment (<see cref="ProcessUsage.Take"/>): its CPU time and each thread's, by thread id and start time.</summary>
public sealed record UsageSnapshot(DateTime At, TimeSpan Cpu, IReadOnlyDictionary<(int Id, DateTime Started), (TimeSpan Cpu, string Name)> Threads,
    RuntimeCounters Runtime, int Handles, long Timers, long WorkingSet, long PrivateBytes)
{
    /// <summary>E.g. "61 threads, 812 handles, 7 timers, working set 196 MB, private 150 MB".</summary>
    public string State => string.Create(CultureInfo.InvariantCulture,
        $"{Threads.Count} threads, {Handles} handles, {Timers} timers, working set {WorkingSet / 1048576} MB, private {PrivateBytes / 1048576} MB");
}

/// <summary>
/// What the process did over some time: its CPU in all and by thread (threads of the same name together), and .NET's
/// counters. Several stretches of time add up (<see cref="Add"/>).
/// </summary>
public sealed class Usage
{
    private const int ThreadsShown = 10;

    private readonly Dictionary<string, (TimeSpan Cpu, int Threads)> _threads = new(StringComparer.Ordinal);

    // Threads that came and went in between: how many started, and the ones there at the start that ended, by name.
    private readonly Dictionary<string, int> _ended = new(StringComparer.Ordinal);
    private int _started;

    public TimeSpan Wall { get; private set; }

    public TimeSpan Cpu { get; private set; }

    public RuntimeCounters Runtime { get; private set; }

    /// <summary>From <paramref name="from"/> to <paramref name="to"/>, or from the process's start when <paramref name="from"/> is null.</summary>
    public static Usage Between(UsageSnapshot? from, UsageSnapshot to)
    {
        var usage = new Usage
        {
            Wall = to.At - (from?.At ?? ProcessUsage.Started),
            Cpu = to.Cpu - (from?.Cpu ?? TimeSpan.Zero),
            Runtime = from is null ? to.Runtime : to.Runtime.Since(from.Runtime),
        };
        var counted = TimeSpan.Zero;
        foreach (var (key, (cpu, name)) in to.Threads)
        {
            var before = from is not null && from.Threads.TryGetValue(key, out var earlier) ? earlier.Cpu : TimeSpan.Zero;
            var (sum, count) = usage._threads.GetValueOrDefault(name);
            usage._threads[name] = (sum + (cpu - before), count + 1);
            counted += cpu - before;
        }
        // Threads that ended in between took their share with them.
        if (usage.Cpu - counted > TimeSpan.FromMilliseconds(16))
            usage._threads["threads that ended"] = (usage.Cpu - counted, 0);
        if (from is not null)
        {
            usage._started = to.Threads.Keys.Count(key => !from.Threads.ContainsKey(key));
            foreach (var (key, (_, name)) in from.Threads)
            {
                if (!to.Threads.ContainsKey(key))
                    usage._ended[name] = usage._ended.GetValueOrDefault(name) + 1;
            }
        }
        return usage;
    }

    public void Add(Usage other)
    {
        Wall += other.Wall;
        Cpu += other.Cpu;
        Runtime = Runtime.Plus(other.Runtime);
        foreach (var (name, (cpu, threads)) in other._threads)
        {
            var (sum, most) = _threads.GetValueOrDefault(name);
            _threads[name] = (sum + cpu, Math.Max(most, threads));
        }
        _started += other._started;
        foreach (var (name, count) in other._ended)
            _ended[name] = _ended.GetValueOrDefault(name) + count;
    }

    /// <summary>Lines for the diagnostics: the CPU, the busiest threads, .NET's activity.</summary>
    public void Describe(StringBuilder text, string indent)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"{indent}CPU: {ProcessUsage.Describe(Cpu, Wall)}");
        var busiest = _threads.Where(t => t.Value.Cpu > TimeSpan.Zero).OrderByDescending(t => t.Value.Cpu).ToList();
        var shown = busiest.Take(ThreadsShown).Select(t => string.Create(CultureInfo.InvariantCulture,
            $"{t.Key}{(t.Value.Threads > 1 ? $" x{t.Value.Threads}" : "")} {ProcessUsage.Percent(t.Value.Cpu, Wall):0.00}"));
        var rest = busiest.Skip(ThreadsShown).ToList();
        var others = rest.Count == 0 ? "" : string.Create(CultureInfo.InvariantCulture,
            $" | {rest.Count} more {ProcessUsage.Percent(rest.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Value.Cpu), Wall):0.00}");
        text.AppendLine(CultureInfo.InvariantCulture, $"{indent}by thread, % of one core: {string.Join(" | ", shown)}{others}");
        if (_started > 0 || _ended.Count > 0)
        {
            var ended = _ended.OrderByDescending(e => e.Value).Select(e => e.Value > 1 ? $"{e.Key} x{e.Value}" : e.Key);
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{indent}threads started meanwhile: {_started}; ended: {_ended.Values.Sum()}{(_ended.Count > 0 ? $" ({string.Join(", ", ended)})" : "")}, besides any that started and ended in between");
        }
        var seconds = Math.Max(1, Wall.TotalSeconds);
        text.AppendLine(CultureInfo.InvariantCulture,
            $"{indent}.NET: {Runtime.Allocated / 1048576.0:0.0} MB allocated ({Runtime.Allocated / 1024.0 / seconds:0.0} KB/s), " +
            $"collections {Runtime.Gen0}/{Runtime.Gen1}/{Runtime.Gen2} (paused {Runtime.GcPause.TotalSeconds:0.00} s), " +
            $"thread pool {Runtime.ThreadPoolItems} items ({Runtime.ThreadPoolItems / seconds:0.0}/s), {Runtime.LockWaits} lock waits");
    }
}
