using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Tests.IntegrationTests.PokeApi;

/// <summary>
/// Hits the live PokeAPI. Slow and network-dependent; tagged as integration so it can be
/// filtered out of the default unit-test run if needed.
/// </summary>
public class PokeApiClientTests : IntegrationTestBase
{
    [Test]
    public async Task GetSpeciesAsync_WithKnownLegendary_ReturnsPopulatedSpecies()
    {
        // Act
        var result = await _fixture.PokeApiClient.GetSpeciesAsync("mewtwo", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data!.Name).IsEqualTo("mewtwo");
        await Assert.That(result.Data.IsLegendary).IsTrue();
        await Assert.That(result.Data.Habitat?.Name).IsEqualTo("rare");
        await Assert.That(result.Data.FlavorTextEntries.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GetSpeciesAsync_WithUnknownName_ReturnsNotFound()
    {
        // Act
        var result = await _fixture.PokeApiClient.GetSpeciesAsync("notarealpokemon123xyz", CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.NotFound);
    }

    [Test]
    public async Task GetSpeciesAsync_SecondCallForSameName_IsServedFromCache()
    {
        // Act
        var first = await _fixture.PokeApiClient.GetSpeciesAsync("pikachu", CancellationToken.None);
        var second = await _fixture.PokeApiClient.GetSpeciesAsync("pikachu", CancellationToken.None);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(second.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(_fixture.PokeApiHandler.RequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetSpeciesAsync_NotFoundResults_AreAlsoCached()
    {
        // Act
        var first = await _fixture.PokeApiClient.GetSpeciesAsync("notarealpokemon123xyz", CancellationToken.None);
        var second = await _fixture.PokeApiClient.GetSpeciesAsync("notarealpokemon123xyz", CancellationToken.None);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(ResultStatus.NotFound);
        await Assert.That(second.StatusCode).IsEqualTo(ResultStatus.NotFound);
        await Assert.That(_fixture.PokeApiHandler.RequestCount).IsEqualTo(1);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task GetSpeciesAsync_WithEmptyName_ReturnsInvalidArgumentWithoutHittingNetwork(string name)
    {
        // Act
        var result = await _fixture.PokeApiClient.GetSpeciesAsync(name, CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.InvalidArgument);
        await Assert.That(_fixture.PokeApiHandler.RequestCount).IsEqualTo(0);
    }
}
