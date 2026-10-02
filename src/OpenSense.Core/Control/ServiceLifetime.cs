namespace OpenSense.Core.Control;

/// <summary>Tracks background work and resume delays so a replaced service cannot keep using its devices.</summary>
internal sealed class ServiceLifetime : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<Task> _work = [];
    private bool _stopped;
    private bool _tokenDisposed;

    public bool Stopped => _stopping.IsCancellationRequested;

    public Task Run(Func<CancellationToken, Task> operation)
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_stopped)
                return Task.CompletedTask;
            _work.RemoveAll(t => t.IsCompleted);
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Add(completion.Task);
        }
        _ = RunAsync();
        return completion.Task;

        async Task RunAsync()
        {
            try
            {
                _stopping.Token.ThrowIfCancellationRequested();
                await operation(_stopping.Token).ConfigureAwait(false);
                completion.TrySetResult();
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                completion.TrySetCanceled(_stopping.Token);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }
    }

    public async Task<T> Run<T>(Func<CancellationToken, Task<T>> operation)
    {
        T result = default!;
        var ran = false;
        await Run(async token =>
        {
            result = await operation(token).ConfigureAwait(false);
            ran = true;
        }).ConfigureAwait(false);
        ObjectDisposedException.ThrowIf(!ran, this);
        return result;
    }

    public async Task StopAsync()
    {
        Task[] work;
        lock (_gate)
        {
            if (!_stopped)
            {
                _stopped = true;
                _stopping.Cancel();
            }
            work = [.. _work];
        }
        try
        {
            await Task.WhenAll(work).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (!_tokenDisposed)
                {
                    _stopping.Dispose();
                    _tokenDisposed = true;
                }
            }
        }
    }

    public void Dispose()
    {
        if (!StopAsync().Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Device work is still stopping; its cancellation source remains in use.");
    }
}
