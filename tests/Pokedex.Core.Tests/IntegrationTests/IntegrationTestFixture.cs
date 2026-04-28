using Microsoft.Extensions.DependencyInjection;
using Pokedex.Core.Infrastructure;
using Pokedex.Core.Services.FunTranslations;
using Pokedex.Core.Services.PokeApi;
using Pokedex.Core.Tests.IntegrationTests.Common;

namespace Pokedex.Core.Tests.IntegrationTests;

/// <summary>
/// Shared integration-test wiring. Builds a real <see cref="ServiceProvider"/> with both
/// typed HTTP clients and a single <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>,
/// mirroring the production DI graph. Each test gets a fresh fixture (so handler counts
/// and cache start clean) and the provider is disposed via
/// <see cref="IAsyncDisposable.DisposeAsync"/> on teardown.
/// </summary>
public class IntegrationTestFixture : IAsyncDisposable
{
    private const string POKE_API_BASE_URL = "https://pokeapi.co/api/v2/";
    private const string FUN_TRANSLATIONS_BASE_URL = "https://funtranslations.mercxry.me/";

    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// Counts outbound requests issued by the resolved <see cref="IPokeApiClient"/>; lets
    /// tests assert that subsequent calls were served from the in-memory cache.
    /// </summary>
    public CountingHandler PokeApiHandler { get; } = new();

    /// <summary>
    /// Counts outbound requests issued by the resolved <see cref="IFunTranslationsClient"/>.
    /// </summary>
    public CountingHandler FunTranslationsHandler { get; } = new();

    /// <summary>
    /// PokeAPI client resolved from the test DI container.
    /// </summary>
    public IPokeApiClient PokeApiClient { get; }

    /// <summary>
    /// FunTranslations client resolved from the test DI container.
    /// </summary>
    public IFunTranslationsClient FunTranslationsClient { get; }

    public IntegrationTestFixture()
    {
        var services = new ServiceCollection();

        // Settings – one section per upstream, short test-scoped TTLs
        services.Configure<PokedexSettings>(options =>
        {
            options.PokeApi = new PokeApiSettings
            {
                BaseUrl = POKE_API_BASE_URL,
                SuccessTtlMinutes = 5,
                NotFoundTtlSeconds = 60,
            };
            options.FunTranslations = new FunTranslationsSettings
            {
                BaseUrl = FUN_TRANSLATIONS_BASE_URL,
                SuccessTtlHours = 24,
            };
        });

        // Single shared cache – cache keys are namespaced per upstream so there's no collision
        services.AddMemoryCache(options => options.SizeLimit = 100);

        // Typed clients – the per-fixture CountingHandler is wired in as the primary handler
        // so every outbound request flows through it and increments the counter.
        services.AddHttpClient<IPokeApiClient, PokeApiClient>(client =>
        {
            client.BaseAddress = new Uri(POKE_API_BASE_URL);
        }).ConfigurePrimaryHttpMessageHandler(() => this.PokeApiHandler);

        services.AddHttpClient<IFunTranslationsClient, FunTranslationsClient>(client =>
        {
            client.BaseAddress = new Uri(FUN_TRANSLATIONS_BASE_URL);
        }).ConfigurePrimaryHttpMessageHandler(() => this.FunTranslationsHandler);

        _serviceProvider = services.BuildServiceProvider();

        // Resolve once and capture so all calls in a single test run against the same client
        // instance (and therefore the same primary handler / counter).
        this.PokeApiClient = _serviceProvider.GetRequiredService<IPokeApiClient>();
        this.FunTranslationsClient = _serviceProvider.GetRequiredService<IFunTranslationsClient>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }
}
