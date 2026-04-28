using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Services.PokeApi;

internal class PokeApiClient : IPokeApiClient
{
    /// <inheritdoc/>
    public Task<Result<PokemonSpecies>> GetSpeciesAsync(string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
