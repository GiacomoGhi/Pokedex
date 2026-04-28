using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pokedex.Core.Services.FunTranslations;
using Pokedex.Core.Services.PokeApi;
using Pokedex.Core.Services.Pokemon;

namespace Pokedex.Core.Infrastructure;

public static class ConfigurationExtensions
{
    private const string CONFIGURATION_SECTION = "Pokedex";

    /// <summary>
    /// Adds Pokedex core services to the service collection: typed HTTP clients, the shared
    /// in-memory cache, the strongly-typed <see cref="PokedexSettings"/> binding and the
    /// service-layer registrations consumed by the API endpoints.
    /// </summary>
    public static IServiceCollection AddPokedexCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Settings
        var section = configuration.GetSection(CONFIGURATION_SECTION);
        var settings = section.Get<PokedexSettings>()
            ?? throw new InvalidOperationException($"Configuration section '{CONFIGURATION_SECTION}' is missing or empty.");
        services.Configure<PokedexSettings>(section);

        // Shared in-memory cache – every cache entry is sized at 1 so the limit is in entry count
        services.AddMemoryCache(options => options.SizeLimit = settings.Cache.MaxEntries);

        // Typed HTTP clients – BaseAddress comes from configuration so dev / prod / mirror
        // swaps require no code changes
        services.AddHttpClient<IPokeApiClient, PokeApiClient>(client =>
        {
            client.BaseAddress = new Uri(settings.PokeApi.BaseUrl);
        });

        services.AddHttpClient<IFunTranslationsClient, FunTranslationsClient>(client =>
        {
            client.BaseAddress = new Uri(settings.FunTranslations.BaseUrl);
        });

        // Service layer
        services.AddScoped<IPokemonService, PokemonService>();

        return services;
    }
}
