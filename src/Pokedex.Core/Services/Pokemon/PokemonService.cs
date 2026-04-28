using System.Text;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Services.FunTranslations;
using Pokedex.Core.Services.PokeApi;

namespace Pokedex.Core.Services.Pokemon;

internal class PokemonService(
    IPokeApiClient pokeApiClient,
    IFunTranslationsClient funTranslationsClient) : IPokemonService
{
    private const string ENGLISH_LANGUAGE_CODE = "en";
    private const string CAVE_HABITAT = "cave";

    private readonly IPokeApiClient _pokeApiClient = pokeApiClient;
    private readonly IFunTranslationsClient _funTranslationsClient = funTranslationsClient;

    /// <inheritdoc/>
    public async Task<Result<Pokemon>> GetBasicAsync(string name, CancellationToken cancellationToken)
    {
        // Fetch species
        var speciesResult = await this.FetchSpeciesAsync(name, cancellationToken);
        if (speciesResult.HasNonSuccessStatusCode)
        {
            return Result.Error(speciesResult);
        }

        return Result.Success(MapToPokemon(speciesResult.Data!));
    }

    /// <inheritdoc/>
    public async Task<Result<Pokemon>> GetTranslatedAsync(string name, CancellationToken cancellationToken)
    {
        // Fetch species
        var speciesResult = await this.FetchSpeciesAsync(name, cancellationToken);
        if (speciesResult.HasNonSuccessStatusCode)
        {
            return Result.Error(speciesResult);
        }

        // Map and choose translation style
        var species = speciesResult.Data!;
        var pokemon = MapToPokemon(species);
        var style = ChooseTranslationStyle(species);

        // Apply translation, fall back to the standard description on any failure
        var translationResult = await _funTranslationsClient.TranslateAsync(style, pokemon.Description, cancellationToken);
        if (!translationResult.HasNonSuccessStatusCode && !string.IsNullOrEmpty(translationResult.Data))
        {
            pokemon.Description = translationResult.Data;
        }

        return Result.Success(pokemon);
    }

    /// <summary>
    /// Validates the requested Pokemon name and forwards the normalised value to the
    /// PokeAPI client. Centralised so both endpoints share the same input contract.
    /// </summary>
    private async Task<Result<PokemonSpecies>> FetchSpeciesAsync(string name, CancellationToken cancellationToken)
    {
        // Check params
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.InvalidArgument(nameof(name));
        }

        var normalisedName = name.Trim().ToLowerInvariant();
        return await _pokeApiClient.GetSpeciesAsync(normalisedName, cancellationToken);
    }

    /// <summary>
    /// Picks <see cref="TranslationStyle.Yoda"/> for legendary or cave-dwelling species,
    /// <see cref="TranslationStyle.Shakespeare"/> for everything else.
    /// </summary>
    private static TranslationStyle ChooseTranslationStyle(PokemonSpecies species)
    {
        if (species.IsLegendary || string.Equals(species.Habitat?.Name, CAVE_HABITAT, StringComparison.OrdinalIgnoreCase))
        {
            return TranslationStyle.Yoda;
        }

        return TranslationStyle.Shakespeare;
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
