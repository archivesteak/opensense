using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>The parts of a control step that <see cref="LoopTimings"/> times, in the order the diagnostics list them.</summary>
internal enum LoopPart
{
    /// <summary>The CPU's own sensor, through PawnIO.</summary>
    CpuSensor,

    /// <summary>Whether the discrete GPU is on, from Windows' records of the device.</summary>
    GpuPowerState,

    /// <summary>The GPU's driver: NVIDIA's library, or what the driver reports to Windows.</summary>
    GpuDriver,

    /// <summary>Sensors read over Acer's WMI interface: temperatures and fan speeds.</summary>
    FirmwareSensors,

    /// <summary>The CPU and GPU load counters.</summary>
    LoadCounters,

    /// <summary>The power supply, operating mode, fans and GPU clocks: deciding, and writing what changed.</summary>
    Control,

    /// <summary>Handing the readings on: the battery's state, and the pipe to the apps.</summary>
    Telemetry,

    /// <summary>Changes the apps asked for, run on the control thread before a step.</summary>
    Requests,
}

/// <summary>
/// Where the control loop's time goes, by <see cref="LoopPart"/>, since it started: for the diagnostics, to find out what
/// costs CPU on a laptop where OpenSense uses more than it should. Wall-clock time, so a part that waits for the firmware
/// counts the wait. Written on the control thread, read from any.
/// </summary>
internal sealed class LoopTimings
{
    private readonly object _gate = new();
    private readonly DateTime _since = DateTime.Now;
    private readonly Totals[] _parts = new Totals[Enum.GetValues<LoopPart>().Length];
    private Totals _steps;

    // The WMI processes when the loop started, for what they have used since.
    private Wmi? _wmi;
    private bool _wmiRead;

    /// <summary>
    /// Acer's firmware methods run in a WMI provider host (WmiPrvSE.exe), called through the WMI service: their CPU is what
    /// the firmware reads cost outside OpenSense's own processes (with any other program's use of WMI).
    /// </summary>
    private sealed record Wmi(DateTime At, Dictionary<(int Id, DateTime Started), TimeSpan> Hosts, Dictionary<(int Id, DateTime Started), TimeSpan> Service)
    {
        public static Wmi Now() => new(DateTime.UtcNow, ProcessUsage.ProcessesNamed("WmiPrvSE"), ProcessUsage.ServiceProcess("Winmgmt"));
    }

    private struct Totals
    {
        public long Count, Ticks, Longest;

        public void Add(long ticks)
        {
            Count++;
            Ticks += ticks;
            Longest = Math.Max(Longest, ticks);
        }
    }

    /// <summary>Reads through <paramref name="read"/>, adding the time it took to <paramref name="part"/>.</summary>
    public T Time<T>(LoopPart part, Func<T> read)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return read();
        }
        finally
        {
            Add(part, start);
        }
    }

    public void Time(LoopPart part, Action run)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            run();
        }
        finally
        {
            Add(part, start);
        }
    }

    /// <summary>Adds the time since <paramref name="start"/> (a <see cref="Stopwatch"/> timestamp) to <paramref name="part"/>.</summary>
    public void Add(LoopPart part, long start)
    {
        var ticks = Stopwatch.GetTimestamp() - start;
        lock (_gate)
            _parts[(int)part].Add(ticks);
    }

    /// <summary>Counts a step (the periodic work, without <see cref="LoopPart.Requests"/>) that began at <paramref name="start"/>.</summary>
    public void AddStep(long start)
    {
        var ticks = Stopwatch.GetTimestamp() - start;
        Wmi? wmi = null;
        if (!_wmiRead)
        {
            _wmiRead = true;
            wmi = Wmi.Now();
        }
        lock (_gate)
        {
            _steps.Add(ticks);
            _wmi ??= wmi;
        }
    }

    /// <summary>A table of the parts: how often each runs per step, how long it takes, and how much of a step it is.</summary>
    public string Describe()
    {
        Totals steps;
        Totals[] parts;
        Wmi? wmi;
        lock (_gate)
        {
            steps = _steps;
            parts = [.. _parts];
            wmi = _wmi;
        }
        var text = new StringBuilder();
        var perStep = Math.Max(1, steps.Count);
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Control loop since {_since:HH:mm:ss}: {steps.Count} steps, {Milliseconds(steps.Ticks) / perStep:0.00} ms each, longest {Milliseconds(steps.Longest):0.00} ms (wall-clock time, waiting for the firmware included)");
        text.AppendLine("  part              runs/step   ms each   longest  ms/step");
        foreach (var part in Enum.GetValues<LoopPart>())
        {
            var totals = parts[(int)part];
            if (totals.Count == 0)
                continue;
            text.AppendLine(CultureInfo.InvariantCulture,
                $"  {Name(part),-16}{(double)totals.Count / perStep,11:0.00}{Milliseconds(totals.Ticks) / totals.Count,10:0.00}{Milliseconds(totals.Longest),10:0.00}{Milliseconds(totals.Ticks) / perStep,9:0.00}");
        }
        if (wmi is not null)
        {
            var now = Wmi.Now();
            text.AppendLine(CultureInfo.InvariantCulture,
                $"WMI since {wmi.At.ToLocalTime():HH:mm:ss} (Acer's firmware methods run in a provider host, called through the WMI service; other programs' use of WMI counts too):");
            text.AppendLine(CultureInfo.InvariantCulture, $"  provider hosts (WmiPrvSE.exe): {Since(wmi.Hosts, now.Hosts, wmi.At, now.At)}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  WMI service: {Since(wmi.Service, now.Service, wmi.At, now.At)}");
        }
        return text.ToString();
    }

    /// <summary>What the processes used from <paramref name="at"/> to <paramref name="until"/>.</summary>
    private static string Since(Dictionary<(int Id, DateTime Started), TimeSpan> then, Dictionary<(int Id, DateTime Started), TimeSpan> now,
        DateTime at, DateTime until)
    {
        if (then.Count == 0 && now.Count == 0)
            return "not readable";
        var (cpu, running, ended) = ProcessUsage.Since(then, at, now);
        return string.Create(CultureInfo.InvariantCulture,
            $"{ProcessUsage.Describe(cpu, until - at)} ({running} running{(ended > 0 ? $", {ended} ended since, their time is missing" : "")})");
    }

    private static string Name(LoopPart part) => part switch
    {
        LoopPart.CpuSensor => "CPU sensor",
        LoopPart.GpuPowerState => "GPU power state",
        LoopPart.GpuDriver => "GPU driver",
        LoopPart.FirmwareSensors => "firmware sensors",
        LoopPart.LoadCounters => "load counters",
        LoopPart.Control => "modes and fans",
        LoopPart.Telemetry => "telemetry out",
        LoopPart.Requests => "app requests",
        _ => part.ToString(),
    };

    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
