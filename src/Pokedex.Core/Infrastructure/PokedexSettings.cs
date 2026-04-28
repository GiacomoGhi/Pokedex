namespace Pokedex.Core.Infrastructure;

/// <summary>
/// Root configuration object bound from the <c>Pokedex</c> section of <c>appsettings.json</c>.
/// </summary>
public class PokedexSettings
{
    /// <summary>
    /// PokeAPI client and cache settings.
    /// </summary>
    public PokeApiSettings PokeApi { get; set; } = default!;

    /// <summary>
    /// FunTranslations client and cache settings.
    /// </summary>
    public FunTranslationsSettings FunTranslations { get; set; } = default!;
}
