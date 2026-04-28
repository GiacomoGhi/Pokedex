using System.Text.Json;

namespace Pokedex.Core.Common.Json;

internal static class SnakeCaseJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
