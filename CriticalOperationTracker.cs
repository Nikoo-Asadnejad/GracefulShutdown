using Logger.Services;

namespace GracefulShutdown;

internal sealed class CriticalOperationTracker()
    : ICriticalOperationTracker, IDisposable
{
    private readonly CancellationTokenSource _hardStopCts = new();
    private readonly object _gate = new();
    private TaskCompletionSource<bool>? _drainCompletion;
    private int _inFlightCount;
    private volatile bool _isDraining;

    public CancellationToken CriticalToken => _hardStopCts.Token;

    public bool IsDraining => _isDraining;

    public int InFlightCount => Volatile.Read(ref _inFlightCount);

    public IDisposable BeginOperation()
    {
        var count = Interlocked.Increment(ref _inFlightCount);
        LoggerService.LogDebug($"Critical operation started. In-flight count: {count}.");
        return new OperationScope(this);
    }

    public void BeginDraining()
    {
        lock (_gate)
        {
            if (_isDraining)
            {
                return;
            }

            _isDraining = true;
            _drainCompletion ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (Volatile.Read(ref _inFlightCount) == 0)
            {
                _drainCompletion.TrySetResult(true);
            }
        }
    }

    public async Task WaitForDrainAsync(CancellationToken cancellationToken)
    {
        BeginDraining();

        TaskCompletionSource<bool> completion;
        lock (_gate)
        {
            completion = _drainCompletion!;
        }

        if (completion.Task.IsCompleted)
        {
            LoggerService.LogWarning("No critical operations in flight; shutting down immediately.");
            return;
        }

        LoggerService.LogWarning(
            $"Waiting up to shutdown drain window for {InFlightCount} in-flight critical operation(s) to complete.");

        await using var registration = cancellationToken.Register(static state =>
        {
            var tracker = (CriticalOperationTracker)state!;
            tracker._hardStopCts.Cancel();
            tracker._drainCompletion?.TrySetResult(false);
        }, this);

        var drained = await completion.Task.ConfigureAwait(false);

        if (drained)
        {
            LoggerService.LogWarning("All critical operations drained. Proceeding with shutdown.");
        }
        else
        {
            LoggerService.LogWarning(
                $"Drain window exceeded with {InFlightCount} critical operation(s) still in flight. Forcing shutdown.");
        }
    }

    private void EndOperation()
    {
        var count = Interlocked.Decrement(ref _inFlightCount);
        LoggerService.LogDebug($"Critical operation completed. In-flight count: {count}.");

        if (count > 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_isDraining)
            {
                _drainCompletion?.TrySetResult(true);
            }
        }
    }

    public void Dispose()
    {
        _hardStopCts.Dispose();
    }

    private sealed class OperationScope(CriticalOperationTracker tracker) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                tracker.EndOperation();
            }
        }
    }
}
