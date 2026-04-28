namespace Pokedex.Core.Infrastructure;

/// <summary>
/// Inbound rate-limit configuration. Applied as a per-remote-IP fixed window limiter on
/// every endpoint.
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Maximum number of requests permitted per <see cref="WindowSeconds"/> per client IP.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Length of the rate-limiting window, in seconds.
    /// </summary>
    public int WindowSeconds { get; set; }
}
