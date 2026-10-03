using Microsoft.UI.Windowing;
using OpenSense.Core.Monitoring;

namespace OpenSense.App.Services;

/// <summary>
/// The app's CPU time, in all and while its window was hidden in the notification area, for the diagnostics: what the app
/// itself costs while nobody looks at it.
/// </summary>
public sealed class AppCpuUse
{
    private readonly object _gate = new();
    private TimeSpan _hiddenCpu, _hiddenTime;
    private (TimeSpan Cpu, TimeSpan Running)? _hiddenSince;

    /// <summary>Follows <paramref name="window"/> being shown and hidden, from now on.</summary>
    public void Watch(AppWindow window)
    {
        Change(window.IsVisible);
        window.Changed += (sender, args) =>
        {
            if (args.DidVisibilityChange)
                Change(sender.IsVisible);
        };
    }

    private void Change(bool visible)
    {
        var now = CpuUse.OfThisProcess();
        lock (_gate)
        {
            if (!visible)
            {
                _hiddenSince ??= now;
            }
            else if (_hiddenSince is { } since)
            {
                _hiddenCpu += now.Cpu - since.Cpu;
                _hiddenTime += now.Running - since.Running;
                _hiddenSince = null;
            }
        }
    }

    /// <summary>The app's lines in the diagnostics.</summary>
    public string Describe()
    {
        var now = CpuUse.OfThisProcess();
        TimeSpan cpu, time;
        lock (_gate)
        {
            (cpu, time) = (_hiddenCpu, _hiddenTime);
            if (_hiddenSince is { } since)
            {
                cpu += now.Cpu - since.Cpu;
                time += now.Running - since.Running;
            }
        }
        var hidden = time > TimeSpan.Zero ? CpuUse.Describe(cpu, time) : "never hidden";
        return $"App process ({Path.GetFileName(Environment.ProcessPath)}): {CpuUse.Describe(now.Cpu, now.Running)}{Environment.NewLine}" +
            $"  while hidden in the notification area: {hidden}{Environment.NewLine}";
    }
}
