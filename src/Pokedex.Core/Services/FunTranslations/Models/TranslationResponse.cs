namespace Pokedex.Core.Services.FunTranslations;

/// <summary>
/// FunTranslations response envelope. Only the translated payload is needed by the
/// service so the non-relevant <c>success</c> object is intentionally omitted.
/// </summary>
public class TranslationResponse
{
    /// <summary>
    /// Translation payload containing the translated text.
    /// </summary>
    public TranslationContents? Contents { get; set; }
}
