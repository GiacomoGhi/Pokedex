using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Infrastructure;

namespace Pokedex.Core.Services.PokeApi;

internal class PokeApiClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<PokedexSettings> settings) : IPokeApiClient
{
    private const string CACHE_KEY_PREFIX = "pokeapi:species:";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;
    private readonly PokeApiSettings _settings = settings.Value.PokeApi;

    /// <inheritdoc/>
    public async Task<Result<PokemonSpecies>> GetSpeciesAsync(string name, CancellationToken cancellationToken)
    {
        // Check params
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.InvalidArgument(nameof(name));
        }

        // Cache lookup – store the Result itself so 404s short-circuit subsequent calls
        var cacheKey = CACHE_KEY_PREFIX + name;
        if (_cache.TryGetValue(cacheKey, out Result<PokemonSpecies> cached))
        {
            return cached;
        }

        // Upstream call
        var result = await this.FetchSpeciesAsync(name, cancellationToken);

        // Cache the outcome with a status-specific TTL; transport errors are never cached
        var entryOptions = this.BuildCacheEntryOptions(result.StatusCode);
        if (entryOptions is not null)
        {
            _cache.Set(cacheKey, result, entryOptions);
        }

        return result;
    }

    /// <summary>
    /// Issues a single GET against <c>/pokemon-species/{name}</c> and translates the HTTP
    /// outcome into a <see cref="Result{PokemonSpecies}"/>. Network and JSON failures are
    /// surfaced as <see cref="ResultStatus.Error"/> so callers can retry without exceptions.
    /// </summary>
    private async Task<Result<PokemonSpecies>> FetchSpeciesAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"pokemon-species/{name}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result.NotFound(name);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result.Error($"PokeAPI returned {(int)response.StatusCode}");
            }

            var species = await response.Content.ReadFromJsonAsync<PokemonSpecies>(_jsonOptions, cancellationToken);
            if (species is null)
            {
                return Result.Error("PokeAPI returned an empty response body");
            }

            return Result.Success(species);
        }
        catch (HttpRequestException ex)
        {
            return Result.Error($"PokeAPI transport error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result.Error($"PokeAPI deserialisation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the appropriate cache entry options for the given outcome, or <c>null</c>
    /// when the result should not be cached (transient transport / deserialisation errors).
    /// </summary>
    private MemoryCacheEntryOptions? BuildCacheEntryOptions(ResultStatus status)
    {
        return status switch
        {
            ResultStatus.Success => new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(_settings.SuccessTtlMinutes),
                Size = 1,
            },
            ResultStatus.NotFound => new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_settings.NotFoundTtlSeconds),
                Size = 1,
            },
            _ => null,
        };
    }
}
