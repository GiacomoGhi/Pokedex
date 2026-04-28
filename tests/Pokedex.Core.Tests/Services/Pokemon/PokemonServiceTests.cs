using NSubstitute;
using Pokedex.Core.Common.Models;
using Pokedex.Core.Services.PokeApi;
using Pokedex.Core.Services.Pokemon;

namespace Pokedex.Core.Tests.Services.Pokemon;

public class PokemonServiceTests
{
    private readonly IPokeApiClient _pokeApiClient;
    private readonly PokemonService _service;

    public PokemonServiceTests()
    {
        _pokeApiClient = Substitute.For<IPokeApiClient>();
        _service = new PokemonService(_pokeApiClient);
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
}
