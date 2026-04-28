using Pokedex.Core.Common.Models;

namespace Pokedex.Core.Services.Pokemon;

public interface IPokemonService
{
    /// <summary>
    /// Fetches a Pokemon by its name and returns the standard description, habitat
    /// and legendary flag.
    /// </summary>
    Task<Result<Pokemon>> GetBasicAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches a Pokemon by its name and returns a fun-translated description.
    /// Yoda translation is applied when the habitat is <c>cave</c> or the Pokemon is
    /// legendary, otherwise Shakespeare. If translation fails the standard description
    /// is returned untouched.
    /// </summary>
    Task<Result<Pokemon>> GetTranslatedAsync(string name, CancellationToken cancellationToken);
}
