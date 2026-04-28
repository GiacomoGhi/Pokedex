namespace Pokedex.Core.Services.PokeApi;

/// <summary>
/// Generic <c>{ name, url }</c> shape used throughout PokeAPI responses to reference
/// other resources (habitat, language, version, etc.).
/// </summary>
public class NamedApiResource
{
    /// <summary>
    /// Resource name.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Absolute URL pointing to the referenced resource.
    /// </summary>
    public string Url { get; set; } = default!;
}
