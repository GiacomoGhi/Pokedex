using System.Text;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Services.PokeApi;

namespace Pokedex.Core.Services.Pokemon;

internal class PokemonService(
    IPokeApiClient pokeApiClient) : IPokemonService
{
    private const string ENGLISH_LANGUAGE_CODE = "en";

    private readonly IPokeApiClient _pokeApiClient = pokeApiClient;

    /// <inheritdoc/>
    public async Task<Result<Pokemon>> GetBasicAsync(string name, CancellationToken cancellationToken)
    {
        // Check params
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.InvalidArgument(nameof(name));
        }

        // Normalise name and call client
        var normalisedName = name.Trim().ToLowerInvariant();
        var speciesResult = await _pokeApiClient.GetSpeciesAsync(normalisedName, cancellationToken);

        // Check result
        if (speciesResult.HasNonSuccessStatusCode)
        {
            return Result.Error(speciesResult);
        }

        return Result.Success(MapToPokemon(speciesResult.Data!));
    }

    /// <inheritdoc/>
    public Task<Result<Pokemon>> GetTranslatedAsync(string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Projects a raw <see cref="PokemonSpecies"/> onto the public <see cref="Pokemon"/> DTO,
    /// extracting the first English flavor text and flattening the habitat reference.
    /// </summary>
    private static Pokemon MapToPokemon(PokemonSpecies species)
        => new()
        {
            Name = species.Name,
            Description = ExtractEnglishDescription(species.FlavorTextEntries),
            Habitat = species.Habitat?.Name,
            IsLegendary = species.IsLegendary,
        };

    /// <summary>
    /// Returns the first English flavor text from the supplied entries, normalised so that
    /// embedded line breaks and form-feed control characters become single spaces. Falls back
    /// to an empty string when no English entry is present.
    /// </summary>
    private static string ExtractEnglishDescription(FlavorTextEntry[] entries)
    {
        var englishEntry = entries.FirstOrDefault(entry => entry.Language?.Name == ENGLISH_LANGUAGE_CODE);
        if (englishEntry is null)
        {
            return string.Empty;
        }

        return NormaliseWhitespace(englishEntry.FlavorText);
    }

    /// <summary>
    /// Collapses runs of whitespace (including newlines and form-feeds emitted by PokeAPI)
    /// into single spaces and trims the result.
    /// </summary>
    private static string NormaliseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasWhitespace = true;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                    lastWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(character);
                lastWasWhitespace = false;
            }
        }

        return builder.ToString().TrimEnd();
    }
}
