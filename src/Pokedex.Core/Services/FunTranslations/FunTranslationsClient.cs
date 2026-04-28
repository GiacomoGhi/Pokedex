using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Services.FunTranslations;

internal class FunTranslationsClient : IFunTranslationsClient
{
    /// <inheritdoc/>
    public Task<Result<string>> TranslateAsync(TranslationStyle style, string text, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
