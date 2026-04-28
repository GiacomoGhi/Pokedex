namespace Pokedex.Core.Infrastructure;

/// <summary>
/// Configuration for the upstream PokeAPI HTTP client and its in-memory cache layer.
/// </summary>
public class PokeApiSettings
{
    /// <summary>
    /// Absolute base URL for the PokeAPI v2 endpoints (must end with a trailing slash so
    /// relative URIs resolve correctly).
    /// </summary>
    public string BaseUrl { get; set; } = default!;

    /// <summary>
    /// Sliding TTL applied to cached successful species responses. Sliding gives popular
    /// Pokemon stay-hot behaviour while still allowing cold entries to expire.
    /// </summary>
    public int SuccessTtlMinutes { get; set; }

    /// <summary>
    /// Absolute TTL applied to negative (404) responses to avoid hammering the upstream
    /// when a misspelled name is retried in a tight loop.
    /// </summary>
    public int NotFoundTtlSeconds { get; set; }
}
