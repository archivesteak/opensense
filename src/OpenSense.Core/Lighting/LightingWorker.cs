using System.Collections.Concurrent;

namespace OpenSense.Core.Lighting;

/// <summary>
/// A thread of its own for the HID and USB lights: their exchanges wait tens of milliseconds between reports, which the
/// firmware's thread (fans, sensors) can't spare. Work runs one item at a time, in order.
/// </summary>
public sealed class LightingWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public LightingWorker()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "OpenSense lighting" };
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Job()
        {
            try
            {
                result.SetResult(work());
            }
            catch (Exception ex)
            {
                result.SetException(ex);
            }
        }
        try
        {
            _queue.Add(Job);
        }
        catch (InvalidOperationException)
        {
            result.SetException(new ObjectDisposedException(nameof(LightingWorker)));
        }
        return result.Task;
    }

    private void Run()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
            job();
    }

    /// <summary>Lets queued work finish (a few seconds at most), then ends the thread.</summary>
    public void Dispose()
    {
        _queue.CompleteAdding();
        if (_thread.Join(TimeSpan.FromSeconds(5)))
            _queue.Dispose();
    }
}
