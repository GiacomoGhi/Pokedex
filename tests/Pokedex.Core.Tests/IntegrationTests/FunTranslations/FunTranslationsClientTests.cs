using Pokedex.Core.Common.Models;
using Pokedex.Core.Services.FunTranslations;

namespace Pokedex.Core.Tests.IntegrationTests.FunTranslations;

/// <summary>
/// Hits the live FunTranslations mirror. Slow and network-dependent; the upstream is
/// deterministic for a given input so the assertions can compare exact translated text.
/// </summary>
public class FunTranslationsClientTests : IntegrationTestBase
{
    private const string SAMPLE_TEXT = "Master Obiwan has lost a planet.";

    [Test]
    public async Task TranslateAsync_WithYoda_ReturnsNonEmptyTranslation()
    {
        // Act
        var result = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Yoda, SAMPLE_TEXT, CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data).IsNotNull();
        await Assert.That(result.Data!.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task TranslateAsync_WithShakespeare_ReturnsNonEmptyTranslation()
    {
        // Act
        var result = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Shakespeare, SAMPLE_TEXT, CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(result.Data).IsNotNull();
        await Assert.That(result.Data!.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task TranslateAsync_SecondCallForSameInput_IsServedFromCache()
    {
        // Act
        var first = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Yoda, SAMPLE_TEXT, CancellationToken.None);
        var second = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Yoda, SAMPLE_TEXT, CancellationToken.None);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(second.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(second.Data).IsEqualTo(first.Data);
        await Assert.That(_fixture.FunTranslationsHandler.RequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task TranslateAsync_DifferentStyles_AreCachedSeparately()
    {
        // Act
        var yoda = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Yoda, SAMPLE_TEXT, CancellationToken.None);
        var shakespeare = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Shakespeare, SAMPLE_TEXT, CancellationToken.None);

        // Assert
        await Assert.That(yoda.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(shakespeare.StatusCode).IsEqualTo(ResultStatus.Success);
        await Assert.That(_fixture.FunTranslationsHandler.RequestCount).IsEqualTo(2);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task TranslateAsync_WithEmptyText_ReturnsInvalidArgumentWithoutHittingNetwork(string text)
    {
        // Act
        var result = await _fixture.FunTranslationsClient.TranslateAsync(TranslationStyle.Yoda, text, CancellationToken.None);

        // Assert
        await Assert.That(result.StatusCode).IsEqualTo(ResultStatus.InvalidArgument);
        await Assert.That(_fixture.FunTranslationsHandler.RequestCount).IsEqualTo(0);
    }
}
