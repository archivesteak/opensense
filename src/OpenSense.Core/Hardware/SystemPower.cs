using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.System.Threading;

namespace OpenSense.Core.Hardware;

/// <summary>A power setting of the active Windows power plan, and the value (index) to use on AC and on battery.</summary>
public sealed record PowerOverride(Guid Subgroup, Guid Setting, uint Value);

/// <summary>What a power setting was before OpenSense changed it.</summary>
public sealed record SavedPowerValue(Guid Subgroup, Guid Setting, uint Ac, uint Dc);

/// <summary>The settings OpenSense changed on a power plan, with their earlier values.</summary>
public sealed record SavedPowerScheme(Guid Scheme, IReadOnlyList<SavedPowerValue> Values);

/// <summary>Windows' power management, as far as a battery calibration needs it.</summary>
public interface ISystemPower
{
    /// <summary>Keeps Windows from sleeping or hibernating on its own until <see cref="ReleaseAwake"/>.</summary>
    void HoldAwake();

    void ReleaseAwake();

    /// <summary>Changes settings of the active power plan; null when Windows refused. The result undoes it.</summary>
    SavedPowerScheme? Override(IReadOnlyList<PowerOverride> changes);

    bool Restore(SavedPowerScheme saved);
}

/// <summary>Windows' power management (works from a service).</summary>
public sealed unsafe class WindowsSystemPower : ISystemPower
{
    private const string Reason = "OpenSense is calibrating the battery";

    private readonly object _gate = new();
    private HANDLE _request;

    public void HoldAwake()
    {
        lock (_gate)
        {
            if (!_request.IsNull)
                return;
            fixed (char* reason = Reason)
            {
                var context = new REASON_CONTEXT
                {
                    Version = PInvoke.POWER_REQUEST_CONTEXT_VERSION,
                    Flags = POWER_REQUEST_CONTEXT_FLAGS.POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                };
                context.Reason.SimpleReasonString = new PWSTR(reason);
                var request = PInvoke.PowerCreateRequest(context);
                if (request.IsInvalid)
                {
                    request.Dispose();
                    return;
                }
                if (!PInvoke.PowerSetRequest(request, POWER_REQUEST_TYPE.PowerRequestSystemRequired))
                {
                    request.Dispose();
                    return;
                }
                _request = (HANDLE)request.DangerousGetHandle();
                request.SetHandleAsInvalid(); // kept open until ReleaseAwake
            }
        }
    }

    public void ReleaseAwake()
    {
        lock (_gate)
        {
            if (_request.IsNull)
                return;
            PInvoke.PowerClearRequest(_request, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
            PInvoke.CloseHandle(_request);
            _request = default;
        }
    }

    public SavedPowerScheme? Override(IReadOnlyList<PowerOverride> changes)
    {
        if (PowerPlans.Active() is not { } scheme)
            return null;
        var saved = new List<SavedPowerValue>();
        foreach (var (subgroup, setting, value) in changes)
        {
            uint ac, dc;
            if (!Ok(PInvoke.PowerReadACValueIndex(default, &scheme, &subgroup, &setting, &ac))
                || !Ok(PInvoke.PowerReadDCValueIndex(default, &scheme, &subgroup, &setting, &dc)))
                continue; // a setting this plan lacks
            saved.Add(new SavedPowerValue(subgroup, setting, ac, dc));
            if (!Ok(PInvoke.PowerWriteACValueIndex(default, &scheme, &subgroup, &setting, value))
                || !Ok(PInvoke.PowerWriteDCValueIndex(default, &scheme, &subgroup, &setting, value)))
            {
                Restore(new SavedPowerScheme(scheme, saved));
                return null;
            }
        }
        var result = new SavedPowerScheme(scheme, saved);
        // Settings of the active plan take effect when the plan is made active again.
        if (!Ok(PInvoke.PowerSetActiveScheme(default, &scheme)))
        {
            Restore(result);
            return null;
        }
        return result;
    }

    public bool Restore(SavedPowerScheme saved)
    {
        var scheme = saved.Scheme;
        var ok = true;
        foreach (var (subgroup, setting, ac, dc) in saved.Values)
        {
            ok &= Ok(PInvoke.PowerWriteACValueIndex(default, &scheme, &subgroup, &setting, ac));
            ok &= Ok(PInvoke.PowerWriteDCValueIndex(default, &scheme, &subgroup, &setting, dc));
        }
        if (PowerPlans.Active() == scheme)
            ok &= Ok(PInvoke.PowerSetActiveScheme(default, &scheme));
        return ok;
    }

    private static bool Ok(WIN32_ERROR result) => result == WIN32_ERROR.ERROR_SUCCESS;

    /// <summary>The battery-power (DC) functions are declared as returning a plain DWORD.</summary>
    private static bool Ok(uint result) => result == (uint)WIN32_ERROR.ERROR_SUCCESS;
}
