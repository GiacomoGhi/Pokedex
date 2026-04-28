namespace Pokedex.Core.Services.Pokemon;

public class Pokemon
{
    /// <summary>
    /// The Pokemon's lowercased name.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Free-form description sourced from the PokeAPI flavor text. May be the original
    /// English description or a translated version when returned by the translated endpoint.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// Habitat name, or <c>null</c> when the upstream species record does not specify one.
    /// </summary>
    public string? Habitat { get; set; }

    /// <summary>
    /// True when the Pokemon is flagged as legendary by the PokeAPI species record.
    /// </summary>
    public bool IsLegendary { get; set; }
}
