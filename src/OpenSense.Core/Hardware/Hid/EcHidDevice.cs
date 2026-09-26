using static OpenSense.Core.Hardware.Hid.EcHidProtocol;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>
/// The embedded controller's HID interface on 2024+ Predators (<see cref="EcHidProtocol"/>). Reads return null and
/// writes false when it doesn't answer. One exchange at a time across the machine: PredatorSense's services lock a
/// named mutex around theirs, and this does too. Not thread-safe: the engine uses it on the control thread.
/// </summary>
public sealed class EcHidDevice : IDisposable
{
    private const int Attempts = 5;
    private const int VersionAttempts = 5;

    /// <summary>How long the controller is given to answer (Acer's services wait this long).</summary>
    private static readonly TimeSpan AnswerDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan VersionRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>After this, go ahead without the lock, as Acer's services do.</summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(1);

    private readonly IHidBus _bus;
    private readonly Mutex? _lock;
    private readonly Action<TimeSpan> _sleep;
    private IHidDevice? _device;

    private EcHidDevice(IHidBus bus, IHidDevice device, Mutex? machineLock, Action<TimeSpan> sleep)
    {
        _bus = bus;
        _device = device;
        _lock = machineLock;
        _sleep = sleep;
        Info = device.Info;
    }

    /// <summary>The mutex Acer's services take around each exchange.</summary>
    public static string MutexName { get; } = $@"Global\ECHIDFeatureVID_{VendorId:X4}&PID_{ProductId:X4}";

    public HidDeviceInfo Info { get; private set; }

    /// <summary>Known once <see cref="ReadVersion"/> has had an answer; status values can't be read before.</summary>
    public EcHidVersion? Version { get; private set; }

    /// <summary>Opens the interface; null when the laptop has none.</summary>
    /// <param name="sleep">Waits between request and answer, and between tries (tests pass a no-op).</param>
    /// <param name="machineLock">Take the mutex Acer's services share (tests leave it out).</param>
    public static EcHidDevice? Open(IHidBus bus, Action<TimeSpan>? sleep = null, bool machineLock = true)
    {
        if (bus.Enumerate().FirstOrDefault(Matches) is not { } info || bus.Open(info) is not { } device)
            return null;
        return new EcHidDevice(bus, device, machineLock ? OpenLock() : null, sleep ?? Thread.Sleep);
    }

    /// <summary>Reads the version: up to five times a second apart, as Acer's service does at start.</summary>
    public EcHidVersion? ReadVersion()
    {
        for (var attempt = 0; attempt < VersionAttempts; attempt++)
        {
            if (attempt > 0)
                _sleep(VersionRetryDelay);
            if (Exchange(StatusRequest(EcHidStatus.Version)) is { } reply && DecodeVersion(reply) is { } version)
                return Version = version;
        }
        return null;
    }

    public ushort? ReadStatus(EcHidStatus type) =>
        Version is { } version && Exchange(StatusRequest(type)) is { } reply ? DecodeStatus(reply, type, version) : null;

    public byte? ReadMode() => Exchange(ModeRequest()) is { } reply ? DecodeMode(reply) : null;

    public bool WriteMode(byte value) => Exchange(SetModeRequest(value)) is { } reply && Accepted(reply, EcHidCommand.Mode);

    public ClockOffsets? ReadOverclockProfile(byte index) =>
        Exchange(OverclockProfileRequest(index)) is { } reply ? DecodeOverclockProfile(reply) : null;

    public (int Brightness, int TimeoutSeconds)? ReadBacklightTimeout() =>
        Exchange(BacklightTimeoutRequest()) is { } reply ? DecodeBacklightTimeout(reply) : null;

    public bool WriteBacklightTimeout(int brightness, int timeoutSeconds) =>
        Exchange(SetBacklightTimeoutRequest(brightness, timeoutSeconds)) is { } reply && Accepted(reply, EcHidCommand.Device);

    /// <summary>
    /// Sends <paramref name="request"/> and reads the answer 20 ms later; asks again (up to five times) while there is
    /// none, or one that is neither done nor refused. Null when none came.
    /// </summary>
    public EcHidReply? Exchange(byte[] request)
    {
        var command = (ushort)(request[3] | request[4] << 8);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
                _sleep(RetryDelay);
            var locked = Lock();
            try
            {
                if (Once(request) is { } reply && reply.Final && reply.Command == command)
                    return reply;
            }
            finally
            {
                if (locked)
                    _lock!.ReleaseMutex();
            }
        }
        return null;
    }

    private EcHidReply? Once(byte[] request)
    {
        if (_device is null && !Reopen())
            return null;
        var length = Math.Max(Info.FeatureLength, ReportLength);
        var report = request.Length == length ? request : Resize(request, length);
        if (!_device!.SetFeature(report))
        {
            // The handle may have gone stale (the device is enumerated again after a resume): open it anew.
            Reopen();
            return null;
        }
        _sleep(AnswerDelay);
        var answer = new byte[length];
        answer[0] = ReportId;
        if (!_device.GetFeature(answer))
        {
            Reopen();
            return null;
        }
        return Decode(answer);
    }

    private bool Reopen()
    {
        _device?.Dispose();
        _device = null;
        if (_bus.Enumerate().FirstOrDefault(Matches) is not { } info || _bus.Open(info) is not { } device)
            return false;
        _device = device;
        Info = info;
        return true;
    }

    private static byte[] Resize(byte[] request, int length)
    {
        var report = new byte[length];
        request.AsSpan(0, Math.Min(request.Length, length)).CopyTo(report);
        return report;
    }

    /// <returns>Whether the lock is held (and must be released).</returns>
    private bool Lock()
    {
        if (_lock is null)
            return false;
        try
        {
            return _lock.WaitOne(LockTimeout);
        }
        catch (AbandonedMutexException)
        {
            return true; // its owner died mid-exchange; the lock is ours now
        }
    }

    /// <summary>Acer's services create it (or open it) under this name; null when neither is allowed.</summary>
    private static Mutex? OpenLock()
    {
        try
        {
            return Mutex.TryOpenExisting(MutexName, out var existing) ? existing : new Mutex(false, MutexName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
        _lock?.Dispose();
    }
}
