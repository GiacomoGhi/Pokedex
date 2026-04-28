namespace Pokedex.Core.Infrastructure;

/// <summary>
/// Global in-memory cache settings shared by every upstream HTTP client.
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// Upper bound on the number of entries kept in the in-memory cache. Each cache entry
    /// is sized at <c>1</c> so this maps directly to the count of cached items, regardless
    /// of payload size.
    /// </summary>
    public int MaxEntries { get; set; }
}
