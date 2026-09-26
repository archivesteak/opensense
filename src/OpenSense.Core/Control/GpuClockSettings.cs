using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Control;

/// <summary>
/// Overclocking the discrete (NVIDIA) GPU: offsets for each operating mode, as in Acer's software, or one set on
/// laptops without operating modes. None set leaves the clocks to the driver, and to tools such as Afterburner.
/// </summary>
public sealed record GpuClockSettings
{
    /// <summary>Offsets per operating mode; a mode without an entry runs at the driver's clocks.</summary>
    public IReadOnlyDictionary<OperatingMode, ClockOffsets> Modes { get; init; } = new Dictionary<OperatingMode, ClockOffsets>();

    /// <summary>The offsets on laptops without operating modes (or with them turned off).</summary>
    public ClockOffsets Fixed { get; init; } = ClockOffsets.None;

    /// <summary>
    /// Add the overclock the firmware gives each operating mode (<see cref="EcHidCapabilities.GpuOffsets"/>, 2024+
    /// Predators), as Acer's software applies it, to the offsets above.
    /// </summary>
    public bool FollowFirmware { get; init; } = true;

    /// <summary>
    /// What applies in <paramref name="mode"/>: its own offsets with operating modes (plus the firmware's for it, when
    /// followed), else <see cref="Fixed"/>.
    /// </summary>
    /// <param name="firmware">The firmware's offsets per mode, where it has them.</param>
    public ClockOffsets For(bool operatingModes, OperatingMode? mode, IReadOnlyDictionary<OperatingMode, ClockOffsets>? firmware = null)
    {
        if (!operatingModes)
            return Fixed;
        if (mode is not { } m)
            return ClockOffsets.None;
        var own = Modes.GetValueOrDefault(m) ?? ClockOffsets.None;
        return FollowFirmware && firmware?.GetValueOrDefault(m) is { } acer ? own.Add(acer) : own;
    }
}

/// <summary>
/// Applies <see cref="GpuClockSettings"/>, on the control thread:
/// <list type="bullet">
/// <item>never asks the driver while the GPU is off, which would wake it: what it knew is forgotten when the GPU goes
/// off or the laptop resumes, and read again before any write once the GPU is back;</item>
/// <item>never writes while OpenSense has nothing to set, so other tools' offsets stay; after its own it writes 0 once;</item>
/// <item>keeps within the driver's limits, and puts its own offsets back to 0 when the engine stops.</item>
/// </list>
/// </summary>
internal sealed class GpuClockController(IGpuClockControl clocks, GpuClockRecord? known)
{
    private GpuClockLimits? _limits = known?.Limits;
    private bool _unsupported = known is { Limits: null };
    private ClockOffsets? _inDriver; // null: not read since the GPU came on
    private bool _readTried;
    private bool _owned; // offsets OpenSense wrote may be in the driver
    private ClockOffsets? _refused;

    /// <summary>The driver said something new about its limits: null when it has no clock offsets for this GPU.</summary>
    public event Action<GpuClockLimits?>? LimitsChanged;

    /// <summary>The driver refused offsets it had reported it accepts.</summary>
    public event Action? Refused;

    /// <param name="wanted">What the settings ask for in the operating mode in force.</param>
    /// <param name="gpuOn">Whether the GPU is on; null when that can't be told, which counts as off.</param>
    /// <param name="reread">Read the driver again (after a resume, when another program may have changed it).</param>
    /// <returns>Null while the driver has no clock offsets for the GPU.</returns>
    public GpuClockTelemetry? Step(ClockOffsets wanted, bool? gpuOn, bool reread)
    {
        var on = gpuOn == true;
        if (!on || reread)
        {
            _inDriver = null;
            _readTried = false;
            _refused = null;
        }
        if (on && !_readTried)
        {
            _readTried = true;
            Read();
        }
        if (_unsupported)
            return null;

        var target = wanted.IsNone ? null : _limits?.Clamp(wanted) ?? wanted;
        // OpenSense's own offsets, else 0 to undo them, else nothing.
        var goal = target ?? (_owned ? ClockOffsets.None : null);
        if (goal is not null && _inDriver is { } current && _limits is not null && goal != current && goal != _refused)
            Write(goal, current);
        if (goal is not null && _inDriver == goal)
            _owned = !goal.IsNone;
        return new GpuClockTelemetry(_limits, on ? _inDriver : null, target);
    }

    /// <summary>The engine stops: OpenSense's offsets back to 0, as the driver would otherwise keep them until it restarts.</summary>
    public void Restore()
    {
        if (!_owned)
            return;
        // Even with the GPU off: waking it for a moment beats leaving it overclocked without OpenSense.
        clocks.Write(GpuClock.Core, 0);
        clocks.Write(GpuClock.Memory, 0);
        _owned = false;
    }

    private void Read()
    {
        if (clocks.Read() is not { } reading)
        {
            if (clocks.Unsupported && !_unsupported)
            {
                _unsupported = true;
                _limits = null;
                LimitsChanged?.Invoke(null);
            }
            return;
        }
        _inDriver = reading.Offsets;
        if (_unsupported || reading.Limits != _limits)
        {
            _unsupported = false;
            _limits = reading.Limits;
            LimitsChanged?.Invoke(_limits);
        }
    }

    private void Write(ClockOffsets goal, ClockOffsets current)
    {
        var core = goal.CoreMhz == current.CoreMhz || clocks.Write(GpuClock.Core, goal.CoreMhz);
        var memory = goal.MemoryMhz == current.MemoryMhz || clocks.Write(GpuClock.Memory, goal.MemoryMhz);
        _inDriver = new ClockOffsets(core ? goal.CoreMhz : current.CoreMhz, memory ? goal.MemoryMhz : current.MemoryMhz);
        if (!_inDriver.IsNone)
            _owned = true;
        if (core && memory)
            return;
        _refused = goal; // not again until the goal changes or the GPU comes back
        Refused?.Invoke();
    }
}
