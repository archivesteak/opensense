using System.Diagnostics;
using System.Globalization;

namespace OpenSense.Core.Monitoring;

/// <summary>How much CPU a process used, worded for the diagnostics.</summary>
public static class CpuUse
{
    /// <summary>This process's CPU time so far, and how long it has been running.</summary>
    public static (TimeSpan Cpu, TimeSpan Running) OfThisProcess()
    {
        using var process = Process.GetCurrentProcess();
        return (process.TotalProcessorTime, DateTime.Now - process.StartTime);
    }

    /// <summary>E.g. "83.2 s of CPU in 21.0 h: 0.11% of one core, 0.009% of all 12".</summary>
    public static string Describe(TimeSpan cpu, TimeSpan running)
    {
        var oneCore = running > TimeSpan.Zero ? cpu / running * 100 : 0;
        return string.Create(CultureInfo.InvariantCulture,
            $"{cpu.TotalSeconds:0.0} s of CPU in {Duration(running)}: {oneCore:0.00}% of one core, {oneCore / Environment.ProcessorCount:0.000}% of all {Environment.ProcessorCount}");
    }

    private static string Duration(TimeSpan time) =>
        time.TotalMinutes < 2 ? string.Create(CultureInfo.InvariantCulture, $"{time.TotalSeconds:0} s")
        : time.TotalHours < 2 ? string.Create(CultureInfo.InvariantCulture, $"{time.TotalMinutes:0} min")
        : string.Create(CultureInfo.InvariantCulture, $"{time.TotalHours:0.0} h");
}
