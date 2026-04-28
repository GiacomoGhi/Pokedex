namespace Pokedex.Api.Endpoints;

public static class Endpoints
{
    /// <summary>
    /// Registers every Pokedex API endpoint group on the supplied
    /// <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    public static void RegisterPokedexApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Pokemon endpoints
        app.MapPokemonEndpoints();
    }
}
