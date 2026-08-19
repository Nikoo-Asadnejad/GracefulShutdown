using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GracefulShutdown;

public static class ServiceCollectionExtensions
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddGracefulShutdown(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GracefulShutdownOptions>(
            configuration.GetSection(GracefulShutdownOptions.SectionName));

        services.AddSingleton<ICriticalOperationTracker, CriticalOperationTracker>();
        services.AddHostedService<GracefulShutdownHostedService>();

        services.AddHealthChecks()
            .AddCheck<ShutdownReadinessHealthCheck>("shutdown", tags: [ReadyTag]);

        return services;
    }
}
