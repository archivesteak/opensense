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
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();
    private bool _stopping;

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
        lock (_gate)
        {
            if (_stopping)
                result.SetException(new ObjectDisposedException(nameof(LightingWorker)));
            else
                _queue.Add(Job);
        }
        return result.Task;
    }

    private void Run()
    {
        try
        {
            foreach (var job in _queue.GetConsumingEnumerable())
                job();
        }
        finally
        {
            _queue.Dispose();
            _completion.TrySetResult();
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (!_stopping)
            {
                _stopping = true;
                _queue.CompleteAdding();
            }
            return _completion.Task;
        }
    }

    /// <summary>Lets queued work finish (a few seconds at most), then ends the thread.</summary>
    public void Dispose()
    {
        if (!StopAsync().Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("The lighting worker is still stopping; its device resources remain in use.");
    }
}
