namespace OpenSense.App.Services;

/// <summary>
/// Input-matching and bounded retry state machine for <see cref="NitroSenseKey"/>.
/// Kept free of Win32 and WinUI dependencies so unit tests can exercise it directly.
/// </summary>
internal sealed class NitroSenseKeyTracker : IDisposable
{
    public const ushort KeyboardScanCode = 0x75;
    public const byte HidMarkerByte1 = 0x81;
    public const byte HidMarkerByte2 = 0xFF;

    internal static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(15),
    ];

    private readonly object _gate = new();
    private readonly Action<Action> _dispatch;
    private readonly IReadOnlyList<TimeSpan> _retryDelays;
    private readonly ITimer _retryTimer;

    private Action? _retryCallback;
    private long _generation;
    private int _retryIndex;
    private bool _enabled;
    private bool _keyDown;
    private bool _disposed;

    public NitroSenseKeyTracker(TimeProvider? time = null, Action<Action>? dispatch = null, IReadOnlyList<TimeSpan>? retryDelays = null)
    {
        var clock = time ?? TimeProvider.System;
        _dispatch = dispatch ?? (static action => action());
        _retryDelays = retryDelays ?? DefaultRetryDelays;
        _retryTimer = clock.CreateTimer(OnRetryTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
                return _enabled && !_disposed;
        }
    }

    public bool HasPendingRetry
    {
        get
        {
            lock (_gate)
                return _enabled && !_disposed && _retryCallback is not null;
        }
    }

    public void Enable()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _enabled = true;
        }
    }

    public void Disable()
    {
        lock (_gate)
        {
            _enabled = false;
            _keyDown = false;
            CancelRetryLocked();
        }
    }

    /// <summary>Clears latched key-down state after missed break events (e.g. lock screen or sleep/resume).</summary>
    public void ResetKeyState()
    {
        lock (_gate)
            _keyDown = false;
    }

    /// <summary>
    /// Tracks a raw keyboard make/break event for E0 75.
    /// Returns true once on the up-to-down transition; held auto-repeat events return false.
    /// </summary>
    public bool HandleKeyboardInput(ushort makeCode, bool isExtendedE0, bool isKeyUp)
    {
        if (makeCode != KeyboardScanCode || !isExtendedE0)
            return false;

        lock (_gate)
        {
            var isDown = !isKeyUp;
            var pressed = isDown && !_keyDown;
            _keyDown = isDown;
            return pressed;
        }
    }

    /// <summary>
    /// Inspects one or more packed raw HID reports from <c>RAWHID.bRawData</c> for Acer's NitroSense signature.
    /// </summary>
    public static bool MatchesHidReports(ReadOnlySpan<byte> payload, int reportSize, int reportCount)
    {
        if (reportSize < 3 || reportCount <= 0 || payload.Length < 3)
            return false;

        var offset = 0;
        for (var i = 0; i < reportCount && payload.Length - offset >= 3; i++)
        {
            var remaining = payload.Length - offset;
            var report = payload.Slice(offset, Math.Min(reportSize, remaining));
            if (report[1] == HidMarkerByte1 && report[2] == HidMarkerByte2)
                return true;
            if (reportSize >= remaining)
                break;
            offset += reportSize;
        }
        return false;
    }

    /// <summary>
    /// Schedules the next bounded retry after a transient registration failure, or returns null when retries are exhausted or disabled.
    /// </summary>
    public TimeSpan? ScheduleRetry(Action retryCallback)
    {
        ArgumentNullException.ThrowIfNull(retryCallback);

        lock (_gate)
        {
            if (!_enabled || _disposed || _retryIndex >= _retryDelays.Count)
            {
                CancelRetryLocked();
                return null;
            }

            var delay = _retryDelays[_retryIndex++];
            _generation++;
            _retryCallback = retryCallback;
            _retryTimer.Change(delay, Timeout.InfiniteTimeSpan);
            return delay;
        }
    }

    public void ResetRetries()
    {
        lock (_gate)
        {
            CancelRetryLocked();
            _retryIndex = 0;
        }
    }

    public void CancelRetry()
    {
        lock (_gate)
            CancelRetryLocked();
    }

    private void CancelRetryLocked()
    {
        _generation++;
        _retryCallback = null;
        if (!_disposed)
            _retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void OnRetryTimerTick(object? state)
    {
        Action? callback;
        long scheduledGeneration;
        lock (_gate)
        {
            if (!_enabled || _disposed || _retryCallback is null)
                return;
            callback = _retryCallback;
            scheduledGeneration = _generation;
            _retryCallback = null;
        }

        _dispatch(() =>
        {
            lock (_gate)
            {
                if (!_enabled || _disposed || _generation != scheduledGeneration)
                    return;
            }
            callback();
        });
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _enabled = false;
            _keyDown = false;
            _generation++;
            _retryCallback = null;
            _retryTimer.Dispose();
        }
    }
}
