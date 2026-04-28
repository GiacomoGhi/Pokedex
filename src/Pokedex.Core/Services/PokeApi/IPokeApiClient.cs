using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Services.PokeApi;

public interface IPokeApiClient
{
    /// <summary>
    /// Fetches the species record for the given Pokemon name from PokeAPI.
    /// Returns <see cref="ResultStatus.NotFound"/> when the upstream answers 404 and
    /// <see cref="ResultStatus.Error"/> for any other transport or deserialisation issue.
    /// </summary>
    Task<Result<PokemonSpecies>> GetSpeciesAsync(string name, CancellationToken cancellationToken);
}
