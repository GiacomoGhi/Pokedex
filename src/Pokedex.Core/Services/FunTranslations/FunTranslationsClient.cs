using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Infrastructure;

namespace Pokedex.Core.Services.FunTranslations;

internal class FunTranslationsClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<PokedexSettings> settings) : IFunTranslationsClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;
    private readonly FunTranslationsSettings _settings = settings.Value.FunTranslations;

    /// <inheritdoc/>
    public Task<Result<string>> TranslateAsync(TranslationStyle style, string text, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
