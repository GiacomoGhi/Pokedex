using Microsoft.AspNetCore.Mvc;
using Pokedex.Api.Extensions;
using Pokedex.Core.Services.Pokemon;

namespace Pokedex.Api.Endpoints;

public static class PokemonEndpoints
{
    /// <summary>
    /// Maps the two Pokemon endpoints under <c>/pokemon</c>: a basic lookup and a
    /// fun-translated lookup.
    /// </summary>
    public static void MapPokemonEndpoints(this IEndpointRouteBuilder app)
    {
        var pokemonGroup = app.MapGroup("/pokemon")
            .WithTags("Pokemon");

        pokemonGroup.MapGet("/translated/{name}", GetTranslatedAsync)
            .WithName("GetTranslatedPokemon")
            .WithDescription("Fetches a Pokemon with a fun-translated description (Yoda for legendary/cave habitat, Shakespeare otherwise)");

        pokemonGroup.MapGet("/{name}", GetBasicAsync)
            .WithName("GetPokemon")
            .WithDescription("Fetches a Pokemon's standard info (name, description, habitat, isLegendary)");
    }

    private static async Task<IResult> GetBasicAsync(
        [FromServices] IPokemonService pokemonService,
        string name,
        CancellationToken cancellationToken)
        => (await pokemonService.GetBasicAsync(name, cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetTranslatedAsync(
        [FromServices] IPokemonService pokemonService,
        string name,
        CancellationToken cancellationToken)
        => (await pokemonService.GetTranslatedAsync(name, cancellationToken)).ToHttpResult();
}
