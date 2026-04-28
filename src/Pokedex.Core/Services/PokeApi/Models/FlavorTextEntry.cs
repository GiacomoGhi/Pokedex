namespace Pokedex.Core.Services.PokeApi;

/// <summary>
/// One entry in the <c>flavor_text_entries</c> array of a PokeAPI species record.
/// </summary>
public class FlavorTextEntry
{
    /// <summary>
    /// Free-form description as printed in the corresponding game version. Often contains
    /// embedded form-feed and newline characters that need to be normalised before display.
    /// </summary>
    public string FlavorText { get; set; } = default!;

    /// <summary>
    /// Language reference; entries are filtered by <c>Name == "en"</c>.
    /// </summary>
    public NamedApiResource Language { get; set; } = default!;

    /// <summary>
    /// Game version that produced the description.
    /// </summary>
    public NamedApiResource Version { get; set; } = default!;
}
