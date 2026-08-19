namespace GracefulShutdown;

/// <summary>
/// Tracks in-flight critical operations (e.g. supplier booking / ticket issuance)
/// so that a shutdown can wait for them to complete before the process exits.
/// </summary>
public interface ICriticalOperationTracker
{
    /// <summary>
    /// A token that stays uncancelled during normal operation and throughout the
    /// drain window. It is cancelled only if the drain timeout is exceeded (hard stop).
    /// Critical operations should pass this token to their downstream calls instead of
    /// the request-abort token, so a client disconnect or shutdown signal cannot tear
    /// them apart mid-flight.
    /// </summary>
    CancellationToken CriticalToken { get; }

    /// <summary>True once a shutdown signal has been received and draining has begun.</summary>
    bool IsDraining { get; }

    /// <summary>Number of critical operations currently in flight.</summary>
    int InFlightCount { get; }

    /// <summary>
    /// Marks the start of a critical operation. Dispose the returned scope when the
    /// operation completes (a <c>using</c> statement guarantees this even on exceptions).
    /// </summary>
    IDisposable BeginOperation();

    /// <summary>
    /// Marks the application as draining. Readiness checks report unhealthy from this
    /// point on. Safe to call more than once.
    /// </summary>
    void BeginDraining();

    /// <summary>
    /// Completes once all in-flight operations have finished, or once
    /// <paramref name="cancellationToken"/> fires (drain timeout / forced stop), whichever
    /// comes first. Also begins draining if it has not already started.
    /// </summary>
    Task WaitForDrainAsync(CancellationToken cancellationToken);
}
