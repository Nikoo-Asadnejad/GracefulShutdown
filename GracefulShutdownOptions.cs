namespace GracefulShutdown;

/// <summary>
/// Options controlling how long the application waits for in-flight critical
/// operations to finish before it shuts down.
/// </summary>
public sealed class GracefulShutdownOptions
{
    public const string SectionName = "GracefulShutdown";

    /// <summary>
    /// Maximum time to wait for in-flight critical operations to drain once a
    /// shutdown signal has been received. If exceeded, remaining operations are
    /// force-cancelled and the host stops anyway.
    /// </summary>
    public int DrainTimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Extra time added on top of <see cref="DrainTimeoutSeconds"/> when setting
    /// the host shutdown timeout, so the host never force-kills the process before
    /// the drain has had its full window.
    /// </summary>
    public int ShutdownTimeoutBufferSeconds { get; set; } = 10;

    public TimeSpan DrainTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds);

    public TimeSpan HostShutdownTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds + ShutdownTimeoutBufferSeconds);
}
