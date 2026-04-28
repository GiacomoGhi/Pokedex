using NSubstitute;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Services.FunTranslations;
using Pokedex.Core.Services.PokeApi;
using Pokedex.Core.Services.Pokemon;

namespace Pokedex.Core.Tests.Services.Pokemon;

public class PokemonServiceTests
{
    private const string STANDARD_DESCRIPTION = "It thinks deeply.";
    private const string TRANSLATED_DESCRIPTION = "Deeply, think it does.";

    private readonly IPokeApiClient _pokeApiClient;
    private readonly IFunTranslationsClient _funTranslationsClient;
    private readonly PokemonService _service;

    public PokemonServiceTests()
    {
        _pokeApiClient = Substitute.For<IPokeApiClient>();
        _funTranslationsClient = Substitute.For<IFunTranslationsClient>();
        _service = new PokemonService(_pokeApiClient, _funTranslationsClient);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task GetBasicAsync_WithEmptyName_ReturnsInvalidArgument(string name)
    {
        // Act
        var result = await _service.GetBasicAsync(name, CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.InvalidArgument);
    }

    [Test]
    public async Task GetBasicAsync_WithUnknownName_ReturnsNotFound()
    {
        // Arrange
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("missing"));

        // Act
        var result = await _service.GetBasicAsync("missing", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.NotFound);
    }

    [Test]
    public async Task GetBasicAsync_WhenClientErrors_PropagatesError()
    {
        // Arrange
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Error("upstream timeout"));

        // Act
        var result = await _service.GetBasicAsync("mewtwo", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Error);
        await Assert.That(result.Message).IsEqualTo("upstream timeout");
    }

    [Test]
    public async Task GetBasicAsync_MapsSpeciesToPokemonDto()
    {
        // Arrange
        var species = new PokemonSpecies
        {
            Name = "mewtwo",
            IsLegendary = true,
            Habitat = new NamedApiResource { Name = "rare", Url = string.Empty },
            FlavorTextEntries =
            [
                new FlavorTextEntry
                {
                    FlavorText = "Japanese description",
                    Language = new NamedApiResource { Name = "ja", Url = string.Empty },
                    Version = new NamedApiResource { Name = "red", Url = string.Empty },
                },
                new FlavorTextEntry
                {
                    FlavorText = "It was created by\na scientist after years of\fhorrific gene splicing\nand DNA engineering experiments.",
                    Language = new NamedApiResource { Name = "en", Url = string.Empty },
                    Version = new NamedApiResource { Name = "red", Url = string.Empty },
                },
            ],
        };
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(species));

        // Act
        var result = await _service.GetBasicAsync("mewtwo", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data).IsNotNull();
        await Assert.That(result.Data!.Name).IsEqualTo("mewtwo");
        await Assert.That(result.Data.Description).IsEqualTo(
            "It was created by a scientist after years of horrific gene splicing and DNA engineering experiments.");
        await Assert.That(result.Data.Habitat).IsEqualTo("rare");
        await Assert.That(result.Data.IsLegendary).IsTrue();
    }

    [Test]
    public async Task GetBasicAsync_WithNullHabitat_MapsToNullHabitat()
    {
        // Arrange
        var species = new PokemonSpecies
        {
            Name = "missingno",
            IsLegendary = false,
            Habitat = null,
            FlavorTextEntries =
            [
                new FlavorTextEntry
                {
                    FlavorText = "A glitch.",
                    Language = new NamedApiResource { Name = "en", Url = string.Empty },
                    Version = new NamedApiResource { Name = "red", Url = string.Empty },
                },
            ],
        };
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(species));

        // Act
        var result = await _service.GetBasicAsync("missingno", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data!.Habitat).IsNull();
    }

    [Test]
    public async Task GetBasicAsync_NormalisesNameBeforeCallingClient()
    {
        // Arrange
        var species = new PokemonSpecies
        {
            Name = "mewtwo",
            FlavorTextEntries = [],
        };
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(species));

        // Act
        await _service.GetBasicAsync("  Mewtwo  ", CancellationToken.None);

        // Assert
        await _pokeApiClient.Received(1).GetSpeciesAsync("mewtwo", Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task GetTranslatedAsync_WithEmptyName_ReturnsInvalidArgument(string name)
    {
        // Act
        var result = await _service.GetTranslatedAsync(name, CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.InvalidArgument);
    }

    [Test]
    public async Task GetTranslatedAsync_WhenSpeciesNotFound_ReturnsNotFoundWithoutCallingTranslator()
    {
        // Arrange
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("missing"));

        // Act
        var result = await _service.GetTranslatedAsync("missing", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.NotFound);
        await _funTranslationsClient
            .DidNotReceive()
            .TranslateAsync(Arg.Any<TranslationStyle>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTranslatedAsync_WithCaveHabitat_UsesYoda()
    {
        // Arrange
        ArrangeSpecies(habitat: "cave", isLegendary: false);
        ArrangeTranslator(TranslationStyle.Yoda, TRANSLATED_DESCRIPTION);

        // Act
        var result = await _service.GetTranslatedAsync("zubat", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data!.Description).IsEqualTo(TRANSLATED_DESCRIPTION);
        await _funTranslationsClient
            .Received(1)
            .TranslateAsync(TranslationStyle.Yoda, STANDARD_DESCRIPTION, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTranslatedAsync_WithLegendary_UsesYoda()
    {
        // Arrange
        ArrangeSpecies(habitat: "rare", isLegendary: true);
        ArrangeTranslator(TranslationStyle.Yoda, TRANSLATED_DESCRIPTION);

        // Act
        var result = await _service.GetTranslatedAsync("mewtwo", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data!.Description).IsEqualTo(TRANSLATED_DESCRIPTION);
        await _funTranslationsClient
            .Received(1)
            .TranslateAsync(TranslationStyle.Yoda, STANDARD_DESCRIPTION, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTranslatedAsync_WhenNotCaveAndNotLegendary_UsesShakespeare()
    {
        // Arrange
        ArrangeSpecies(habitat: "grassland", isLegendary: false);
        ArrangeTranslator(TranslationStyle.Shakespeare, TRANSLATED_DESCRIPTION);

        // Act
        var result = await _service.GetTranslatedAsync("pikachu", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data!.Description).IsEqualTo(TRANSLATED_DESCRIPTION);
        await _funTranslationsClient
            .Received(1)
            .TranslateAsync(TranslationStyle.Shakespeare, STANDARD_DESCRIPTION, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTranslatedAsync_WithNullHabitat_UsesShakespeareWhenNotLegendary()
    {
        // Arrange
        ArrangeSpecies(habitat: null, isLegendary: false);
        ArrangeTranslator(TranslationStyle.Shakespeare, TRANSLATED_DESCRIPTION);

        // Act
        var result = await _service.GetTranslatedAsync("missingno", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await _funTranslationsClient
            .Received(1)
            .TranslateAsync(TranslationStyle.Shakespeare, STANDARD_DESCRIPTION, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTranslatedAsync_WhenTranslationFails_FallsBackToStandardDescription()
    {
        // Arrange
        ArrangeSpecies(habitat: "grassland", isLegendary: false);
        _funTranslationsClient
            .TranslateAsync(Arg.Any<TranslationStyle>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Error("rate limited"));

        // Act
        var result = await _service.GetTranslatedAsync("pikachu", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data!.Description).IsEqualTo(STANDARD_DESCRIPTION);
    }

    /// <summary>
    /// Configures the mocked PokeAPI client to return a species record with the supplied
    /// habitat and legendary flag, populated with a single English flavor text equal to
    /// <see cref="STANDARD_DESCRIPTION"/>.
    /// </summary>
    private void ArrangeSpecies(string? habitat, bool isLegendary)
    {
        var species = new PokemonSpecies
        {
            Name = "stub",
            IsLegendary = isLegendary,
            Habitat = habitat is null
                ? null
                : new NamedApiResource { Name = habitat, Url = string.Empty },
            FlavorTextEntries =
            [
                new FlavorTextEntry
                {
                    FlavorText = STANDARD_DESCRIPTION,
                    Language = new NamedApiResource { Name = "en", Url = string.Empty },
                    Version = new NamedApiResource { Name = "red", Url = string.Empty },
                },
            ],
        };
        _pokeApiClient
            .GetSpeciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(species));
    }

    /// <summary>
    /// Configures the mocked translator to respond successfully with <paramref name="translation"/>
    /// only when invoked with the requested <paramref name="style"/>.
    /// </summary>
    private void ArrangeTranslator(TranslationStyle style, string translation)
    {
        _funTranslationsClient
            .TranslateAsync(style, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(translation));
    }
}
