using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GracefulShutdown;

/// <summary>
/// Drives graceful shutdown: flips the app to draining the moment a shutdown signal
/// arrives, then blocks shutdown until in-flight critical operations finish (bounded
/// by the configured drain timeout).
/// </summary>
internal sealed class GracefulShutdownHostedService(
    ICriticalOperationTracker tracker,
    IHostApplicationLifetime lifetime,
    IOptions<GracefulShutdownOptions> options,
    ILogger<GracefulShutdownHostedService> logger) : IHostedService
{
    private readonly GracefulShutdownOptions _options = options.Value;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStopping.Register(() =>
        {
            logger.LogInformation("Shutdown signal received. Marking application as draining.");
            tracker.BeginDraining();
        });

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.DrainTimeout);

        await tracker.WaitForDrainAsync(linked.Token).ConfigureAwait(false);
    }
}
