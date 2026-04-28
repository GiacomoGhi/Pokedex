using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Services.Pokemon;

internal class PokemonService : IPokemonService
{
    /// <inheritdoc/>
    public Task<Result<Pokemon>> GetBasicAsync(string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<Result<Pokemon>> GetTranslatedAsync(string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
