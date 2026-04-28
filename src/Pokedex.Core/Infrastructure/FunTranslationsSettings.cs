namespace Pokedex.Core.Infrastructure;

/// <summary>
/// Configuration for the FunTranslations HTTP client and its in-memory cache layer.
/// </summary>
public class FunTranslationsSettings
{
    /// <summary>
    /// Absolute base URL for the FunTranslations API (must end with a trailing slash so
    /// relative URIs resolve correctly). Typically points at the public mirror.
    /// </summary>
    public string BaseUrl { get; set; } = default!;

    /// <summary>
    /// Absolute TTL applied to cached successful translations. Long by default because the
    /// upstream output is deterministic for a given input and the free tier is heavily
    /// rate-limited.
    /// </summary>
    public int SuccessTtlHours { get; set; }
}
