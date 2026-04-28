using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pokedex.Core.Common.Caching;
using Pokedex.Core.Common.Json;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Infrastructure;

namespace Pokedex.Core.Services.PokeApi;

internal class PokeApiClient(
    HttpClient httpClient,
    SingleFlightCache cache,
    IOptions<PokedexSettings> settings) : IPokeApiClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly SingleFlightCache _cache = cache;
    private readonly PokeApiSettings _settings = settings.Value.PokeApi;

    /// <inheritdoc/>
    public async Task<Result<PokemonSpecies>> GetSpeciesAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.InvalidArgument(nameof(name));
        }

        var normalisedName = name.ToLowerInvariant();
        var cacheKey = SingleFlightCache.PokeApiSpeciesPrefix + normalisedName;

        return await _cache.GetOrCreateAsync(
            cacheKey,
            ct => this.FetchSpeciesAsync(normalisedName, ct),
            result => this.BuildCacheEntryOptions(result.StatusCode),
            cancellationToken);
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

            var species = await response.Content.ReadFromJsonAsync<PokemonSpecies>(SnakeCaseJson.Options, cancellationToken);
            if (species is null)
            {
                return Result.Error("PokeAPI returned an empty response body");
            }

            return Result.Success(species);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Error("PokeAPI request timed out");
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
