using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pokedex.Core.Common.Caching;
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
        // Settings — validate at startup so a misconfigured BaseUrl fails loudly at boot
        // rather than silently 404-ing on the first request.
        var section = configuration.GetSection(CONFIGURATION_SECTION);
        var settings = section.Get<PokedexSettings>()
            ?? throw new InvalidOperationException($"Configuration section '{CONFIGURATION_SECTION}' is missing or empty.");

        services.AddOptions<PokedexSettings>()
            .Bind(section)
            .Validate(
                s => IsAbsoluteUrlWithTrailingSlash(s.PokeApi?.BaseUrl),
                "Pokedex:PokeApi:BaseUrl must be an absolute URL ending with '/'.")
            .Validate(
                s => IsAbsoluteUrlWithTrailingSlash(s.FunTranslations?.BaseUrl),
                "Pokedex:FunTranslations:BaseUrl must be an absolute URL ending with '/'.")
            .Validate(
                s => s.PokeApi.SuccessTtlMinutes > 0 && s.PokeApi.NotFoundTtlSeconds > 0,
                "Pokedex:PokeApi TTL values must be positive.")
            .Validate(
                s => s.FunTranslations.SuccessTtlHours > 0,
                "Pokedex:FunTranslations:SuccessTtlHours must be positive.")
            .Validate(
                s => s.Cache.MaxEntries > 0,
                "Pokedex:Cache:MaxEntries must be positive.")
            .Validate(
                s => s.RateLimit.PermitLimit > 0 && s.RateLimit.WindowSeconds > 0,
                "Pokedex:RateLimit values must be positive.")
            .ValidateOnStart();

        // Shared in-memory cache – every cache entry is sized at 1 so the limit is in entry count.
        // SingleFlightCache wraps it with per-key single-flight semantics (no cache stampede).
        services.AddMemoryCache(options => options.SizeLimit = settings.Cache.MaxEntries);
        services.AddSingleton<SingleFlightCache>();

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

    private static bool IsAbsoluteUrlWithTrailingSlash(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out _) && url!.EndsWith('/');
}
