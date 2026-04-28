namespace Pokedex.Core.Services.FunTranslations;

/// <summary>
/// <c>contents</c> sub-object of a FunTranslations response.
/// </summary>
public class TranslationContents
{
    /// <summary>
    /// Translated version of the submitted text. May be null/empty when the upstream
    /// could not produce a translation for the input.
    /// </summary>
    public string? Translated { get; set; }
}
