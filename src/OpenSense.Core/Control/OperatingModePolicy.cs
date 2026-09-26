using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Control;

/// <summary>What the Mode key does (a choice Acer's software offers on laptops with the key).</summary>
public enum ModeKeyAction
{
    /// <summary>Steps through the modes: Balanced, Performance, Turbo, Quiet on AC; Balanced and Eco on battery.</summary>
    Cycle,

    /// <summary>Turns Turbo on, and off again back to the mode before.</summary>
    TurboToggle,
}

/// <summary>How the power supply limits the operating modes.</summary>
public enum PowerLimit
{
    /// <summary>On AC: every mode.</summary>
    None,

    /// <summary>On battery: Eco, Quiet and Balanced (Quiet and Balanced under <see cref="ModeRules.EmbeddedController"/>).</summary>
    Battery,

    /// <summary>On AC, but the battery is too low to help the adapter (the firmware's battery-boost flag is off): Quiet and Balanced.</summary>
    LowBattery,

    /// <summary>
    /// On AC, but the adapter can't power every mode: a USB-C adapter the embedded controller calls too weak, or the
    /// controller's own limit. Quiet and Balanced.
    /// </summary>
    Adapter,
}

/// <summary>Whose rules the operating modes follow.</summary>
public enum ModeRules
{
    /// <summary>Acer's gaming software's (NitroSense, PredatorSense).</summary>
    Gaming,

    /// <summary>
    /// The embedded controller's, on 2024+ Predators whose HID interface sets the mode (as Acer Quick Access applies
    /// them): Quiet and Balanced only whenever power is short, and the Mode key steps through every allowed mode.
    /// </summary>
    EmbeddedController,
}

/// <summary>A press of the Mode key: from <paramref name="Current"/> to <paramref name="Next"/> (null: nowhere to go).</summary>
public readonly record struct ModeKeyPress(OperatingMode? Current, OperatingMode? Next, PowerLimit Limit);

/// <summary>
/// Which operating modes the power supply allows, which one runs, and where the Mode key goes (Acer's rules). Pure.
/// </summary>
public static class OperatingModePolicy
{
    private static readonly OperatingMode[] BatteryModes = [OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced];
    private static readonly OperatingMode[] ShortPowerModes = [OperatingMode.Quiet, OperatingMode.Balanced];

    /// <summary>The Mode key's order on AC.</summary>
    private static readonly OperatingMode[] KeyCycle = [OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo, OperatingMode.Quiet];

    /// <summary>The embedded controller's order, which its Mode key follows round.</summary>
    private static readonly OperatingMode[] ControllerOrder =
        [OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo];

    /// <param name="batteryBoost">The firmware's battery-boost flag; null where it has none.</param>
    /// <param name="weakAdapter">The embedded controller holds the modes back for the adapter; null where it doesn't say.</param>
    public static PowerLimit Limit(bool onAc, bool? batteryBoost, bool? weakAdapter = null) =>
        !onAc ? PowerLimit.Battery
        : weakAdapter == true ? PowerLimit.Adapter
        : batteryBoost == false ? PowerLimit.LowBattery
        : PowerLimit.None;

    /// <summary>
    /// Whether the embedded controller holds the modes back on AC: its own limit, or a USB-C adapter (no barrel
    /// adapter) it doesn't call strong enough. Values that didn't come count as no limit.
    /// </summary>
    public static bool WeakAdapter(ushort? adapter, ushort? modeLimit, ushort? usbCAdapter) =>
        modeLimit is > 0 || (adapter is { } a && !EcHidProtocol.BarrelAdapter(a) && usbCAdapter is { } usbC && usbC != 1);

    /// <summary>The supported modes the power supply allows now, in the order given.</summary>
    public static IReadOnlyList<OperatingMode> Allowed(IReadOnlyCollection<OperatingMode> supported, PowerLimit limit,
        ModeRules rules = ModeRules.Gaming) => limit switch
    {
        PowerLimit.None => [.. supported],
        PowerLimit.Battery when rules == ModeRules.Gaming => [.. supported.Where(BatteryModes.Contains)],
        _ => [.. supported.Where(ShortPowerModes.Contains)],
    };

    /// <summary>
    /// The mode chosen for this power supply: the battery mode on battery (the AC mode where none is chosen), else the
    /// AC mode. Null when the user has chosen none (the firmware is left alone).
    /// </summary>
    public static OperatingMode? Wanted(ControlProfile profile, PowerLimit limit) =>
        limit == PowerLimit.Battery ? profile.BatteryOperatingMode ?? profile.OperatingMode : profile.OperatingMode;

    /// <summary>
    /// The mode to run: <see cref="Wanted"/> when allowed, else Balanced (a mode the power supply rules out gives way
    /// to it, as Acer's software does; Quiet stays Quiet).
    /// </summary>
    public static OperatingMode? Target(ControlProfile profile, IReadOnlyCollection<OperatingMode> supported, PowerLimit limit,
        ModeRules rules = ModeRules.Gaming)
    {
        if (Wanted(profile, limit) is not { } wanted)
            return null;
        var allowed = Allowed(supported, limit, rules);
        if (allowed.Contains(wanted))
            return wanted;
        return allowed.Contains(OperatingMode.Balanced) ? OperatingMode.Balanced : allowed.Count > 0 ? allowed[0] : null;
    }

    /// <summary>
    /// Where the Mode key goes from <paramref name="current"/>: on AC through <see cref="KeyCycle"/> (skipping modes the
    /// laptop lacks) or Turbo and back; on battery between Balanced and Eco (Quiet on laptops without Eco); with the
    /// battery low or a weak adapter, between Balanced and Quiet. Under <see cref="ModeRules.EmbeddedController"/>, the
    /// next allowed mode in the controller's order, round again after the last. Null when it has nowhere to go (then
    /// <see cref="TurboBlocked"/> may say why).
    /// </summary>
    public static OperatingMode? NextForKey(ControlProfile profile, OperatingMode? current, IReadOnlyCollection<OperatingMode> supported,
        PowerLimit limit, ModeRules rules = ModeRules.Gaming)
    {
        if (TurboBlocked(profile, limit))
            return null;
        if (rules == ModeRules.EmbeddedController && profile.ModeKey == ModeKeyAction.Cycle)
            return NextInControllerOrder(current, Allowed(supported, limit, rules));
        switch (limit)
        {
            case PowerLimit.LowBattery or PowerLimit.Adapter:
                return Toggle(current, OperatingMode.Balanced, OperatingMode.Quiet, supported);
            case PowerLimit.Battery:
                return Toggle(current, OperatingMode.Balanced, supported.Contains(OperatingMode.Eco) ? OperatingMode.Eco : OperatingMode.Quiet, supported);
        }

        if (profile.ModeKey == ModeKeyAction.TurboToggle && supported.Contains(OperatingMode.Turbo))
        {
            if (current != OperatingMode.Turbo)
                return OperatingMode.Turbo;
            var back = profile.TurboReturnMode is { } mode && mode != OperatingMode.Turbo && supported.Contains(mode) ? mode : OperatingMode.Balanced;
            return supported.Contains(back) ? back : null;
        }

        var cycle = KeyCycle.Where(supported.Contains).ToList();
        if (cycle.Count == 0)
            return null;
        var index = current is { } now ? cycle.IndexOf(now) : -1;
        // From a mode outside the cycle (Eco on AC), back to its start.
        var next = index < 0 ? cycle[0] : cycle[(index + 1) % cycle.Count];
        return next == current ? null : next;
    }

    /// <summary>The Mode key is set to Turbo, which the power supply doesn't allow now.</summary>
    public static bool TurboBlocked(ControlProfile profile, PowerLimit limit) =>
        profile.ModeKey == ModeKeyAction.TurboToggle && limit != PowerLimit.None;

    /// <summary>
    /// The profile after the Mode key chose <paramref name="next"/>: the battery mode on battery, else the AC mode (and,
    /// going into Turbo with the key, the mode to go back to).
    /// </summary>
    public static ControlProfile WithKeyChoice(ControlProfile profile, OperatingMode? current, OperatingMode next, PowerLimit limit) =>
        limit == PowerLimit.Battery
            ? profile with { BatteryOperatingMode = next }
            : profile with
            {
                OperatingMode = next,
                TurboReturnMode = next == OperatingMode.Turbo && current is { } from && from != OperatingMode.Turbo ? from : profile.TurboReturnMode,
            };

    /// <summary>The next of <paramref name="allowed"/> after <paramref name="current"/>; from a mode outside them, Balanced.</summary>
    private static OperatingMode? NextInControllerOrder(OperatingMode? current, IReadOnlyList<OperatingMode> allowed)
    {
        var order = ControllerOrder.Where(allowed.Contains).ToList();
        if (order.Count == 0)
            return null;
        var index = current is { } now ? order.IndexOf(now) : -1;
        var next = index >= 0 ? order[(index + 1) % order.Count]
            : order.Contains(OperatingMode.Balanced) ? OperatingMode.Balanced
            : order[0];
        return next == current ? null : next;
    }

    private static OperatingMode? Toggle(OperatingMode? current, OperatingMode first, OperatingMode second, IReadOnlyCollection<OperatingMode> supported)
    {
        var next = current == first ? second : first;
        if (!supported.Contains(next))
            next = first;
        return supported.Contains(next) && next != current ? next : null;
    }
}
