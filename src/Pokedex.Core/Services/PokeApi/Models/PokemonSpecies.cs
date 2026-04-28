namespace Pokedex.Core.Services.PokeApi;

/// <summary>
/// PokeAPI <c>/pokemon-species/{name}</c> response, trimmed to the fields the Pokedex
/// service needs to assemble a basic Pokemon DTO.
/// </summary>
public class PokemonSpecies
{
    /// <summary>
    /// Species name (lowercased by PokeAPI).
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Localised flavor text entries; the service picks the first English entry.
    /// </summary>
    public FlavorTextEntry[] FlavorTextEntries { get; set; } = [];

    /// <summary>
    /// Habitat reference. <c>null</c> for species without a defined habitat.
    /// </summary>
    public NamedApiResource? Habitat { get; set; }

    /// <summary>
    /// True when the species is flagged as legendary.
    /// </summary>
    public bool IsLegendary { get; set; }
}
