using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GracefulShutdown;

/// <summary>
/// Readiness health check that reports unhealthy as soon as a shutdown signal has
/// been received, so load balancers stop routing new traffic while in-flight
/// critical operations drain.
/// </summary>
internal sealed class ShutdownReadinessHealthCheck(ICriticalOperationTracker tracker) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(tracker.IsDraining
            ? HealthCheckResult.Unhealthy("Application is shutting down.")
            : HealthCheckResult.Healthy());
    }
}
