using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Services.FunTranslations;

public interface IFunTranslationsClient
{
    /// <summary>
    /// Translates the supplied text using the requested style. Any upstream failure
    /// (rate limit, transport, deserialisation, missing translation) is surfaced as
    /// <see cref="ResultStatus.Error"/> so the caller can fall back to the original text.
    /// </summary>
    Task<Result<string>> TranslateAsync(TranslationStyle style, string text, CancellationToken cancellationToken);
}
