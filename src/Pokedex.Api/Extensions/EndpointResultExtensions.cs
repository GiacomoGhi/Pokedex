using Pokedex.Core.Common.Models;

namespace Pokedex.Api.Extensions;

public static class EndpointResultExtensions
{
    /// <summary>
    /// Converts a service-layer <see cref="Result"/> into the appropriate HTTP response.
    /// </summary>
    public static IResult ToHttpResult(this Result result)
        => ((Result<object>)result).ToHttpResult();

    /// <inheritdoc cref="ToHttpResult(Result)"/>
    public static IResult ToHttpResult<T>(this Result<T> result) where T : class
    {
        if (!result.HasNonSuccessStatusCode)
        {
            return Results.Ok(result.Data);
        }

        return result.StatusCode switch
        {
            ResultStatus.InvalidArgument => Results.BadRequest(new { error = result.Message }),
            ResultStatus.NotFound => Results.NotFound(new { error = result.Message }),
            ResultStatus.Unauthorized => Results.Unauthorized(),
            ResultStatus.Forbidden => Results.Forbid(),
            _ => Results.BadRequest(new { error = result.Message }),
        };
    }
}
