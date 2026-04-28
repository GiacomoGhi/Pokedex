namespace Pokedex.Core.Services.FunTranslations;

/// <summary>
/// Supported FunTranslations endpoints exposed by <see cref="IFunTranslationsClient"/>.
/// </summary>
public enum TranslationStyle
{
    /// <summary>
    /// Yoda-style speech translation – applied to legendary or cave-dwelling Pokemon.
    /// </summary>
    Yoda,

    /// <summary>
    /// Shakespeare-style speech translation – applied to all other Pokemon.
    /// </summary>
    Shakespeare,
}
