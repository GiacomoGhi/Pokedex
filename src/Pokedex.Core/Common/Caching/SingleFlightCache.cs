using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Pokedex.Core.Common.Caching;

/// <summary>
/// Wraps <see cref="IMemoryCache"/> with single-flight semantics: concurrent callers that
/// arrive while a cache key is cold are queued behind a per-key <see cref="SemaphoreSlim"/>
/// so only one upstream fetch fires, then all waiters receive the cached result. This
/// prevents the cache-stampede pattern where N simultaneous cold-cache requests all hit
/// the upstream at once.
///
/// One semaphore object per unique key is leaked for the lifetime of the process; the
/// semaphore count is bounded by the cache's own entry-count limit so the leak is small
/// (≈ 88 bytes × MaxEntries at most).
/// </summary>
public sealed class SingleFlightCache(IMemoryCache cache)
{
    private readonly IMemoryCache _cache = cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _inflight = new();

    /// <summary>
    /// Key prefix for PokeAPI species records.
    /// </summary>
    public const string PokeApiSpeciesPrefix = "pokeapi:species:";

    /// <summary>
    /// Key prefix for FunTranslations results, suffixed with style and text hash.
    /// </summary>
    public const string FunTranslationsPrefix = "funtranslations:";

    /// <summary>
    /// Returns a cached value, or calls <paramref name="factory"/> exactly once when the
    /// key is cold (even under concurrent load), then stores the result according to
    /// <paramref name="buildOptions"/>.
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        Func<T, MemoryCacheEntryOptions?> buildOptions,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var gate = _inflight.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Double-check: another waiter may have populated the cache while we queued.
            if (_cache.TryGetValue(key, out cached) && cached is not null)
            {
                return cached;
            }

            var result = await factory(cancellationToken);

            var options = buildOptions(result);
            if (options is not null)
            {
                _cache.Set(key, result, options);
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}
