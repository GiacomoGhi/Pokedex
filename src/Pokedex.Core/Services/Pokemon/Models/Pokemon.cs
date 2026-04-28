namespace Pokedex.Core.Services.Pokemon;

public record Pokemon
{
    /// <summary>
    /// The Pokemon's lowercased name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Free-form description sourced from the PokeAPI flavor text. May be the original
    /// English description or a translated version when returned by the translated endpoint.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Habitat name, or <c>null</c> when the upstream species record does not specify one.
    /// </summary>
    public string? Habitat { get; init; }

    /// <summary>
    /// True when the Pokemon is flagged as legendary by the PokeAPI species record.
    /// </summary>
    public bool IsLegendary { get; init; }
}
