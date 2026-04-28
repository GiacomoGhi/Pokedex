using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pokedex.Core.Common.Caching;
using Pokedex.Core.Common.Json;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Infrastructure;

namespace Pokedex.Core.Services.FunTranslations;

internal class FunTranslationsClient(
    HttpClient httpClient,
    SingleFlightCache cache,
    IOptions<PokedexSettings> settings) : IFunTranslationsClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly SingleFlightCache _cache = cache;
    private readonly FunTranslationsSettings _settings = settings.Value.FunTranslations;

    /// <inheritdoc/>
    public async Task<Result<string>> TranslateAsync(TranslationStyle style, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.InvalidArgument(nameof(text));
        }

        var cacheKey = BuildCacheKey(style, text);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            ct => this.FetchTranslationAsync(style, text, ct),
            result => result.HasNonSuccessStatusCode ? null : new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_settings.SuccessTtlHours),
                Size = 1,
            },
            cancellationToken);
    }

    /// <summary>
    /// Issues a single POST against <c>/translate/{style}</c> with form-encoded
    /// <c>text</c> body and unwraps the nested <c>contents.translated</c> field. Network,
    /// non-2xx and JSON failures all collapse to <see cref="ResultStatus.Error"/> so the
    /// service layer can fall back to the standard description without exception handling.
    /// </summary>
    private async Task<Result<string>> FetchTranslationAsync(TranslationStyle style, string text, CancellationToken cancellationToken)
    {
        try
        {
            using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["text"] = text,
            });
            using var response = await _httpClient.PostAsync($"translate/{StyleToPath(style)}", formContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Error($"FunTranslations returned {(int)response.StatusCode}");
            }

            var translation = await response.Content.ReadFromJsonAsync<TranslationResponse>(SnakeCaseJson.Options, cancellationToken);
            var translated = translation?.Contents?.Translated;
            if (string.IsNullOrEmpty(translated))
            {
                return Result.Error("FunTranslations returned an empty translation");
            }

            return Result.Success(translated);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Error("FunTranslations request timed out");
        }
        catch (HttpRequestException ex)
        {
            return Result.Error($"FunTranslations transport error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result.Error($"FunTranslations deserialisation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a fixed-length cache key from the translation style and a SHA-256 digest of
    /// the input text, keeping keys compact regardless of how long the source description
    /// is.
    /// </summary>
    private static string BuildCacheKey(TranslationStyle style, string text)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"{SingleFlightCache.FunTranslationsPrefix}{StyleToPath(style)}:{Convert.ToHexString(digest)}";
    }

    /// <summary>
    /// Maps a <see cref="TranslationStyle"/> onto the URL segment expected by the
    /// FunTranslations API.
    /// </summary>
    private static string StyleToPath(TranslationStyle style) => style switch
    {
        TranslationStyle.Yoda => "yoda",
        TranslationStyle.Shakespeare => "shakespeare",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported translation style"),
    };
}
