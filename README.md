# Pokedex API

A small REST API that returns Pokemon information, optionally with a fun-translated
description. Backed by the public [PokeAPI](https://pokeapi.co) and the
[FunTranslations](https://funtranslations.mercxry.me) mirror.

## Endpoints

| Method | Path                              | Description                                            |
| ------ | --------------------------------- | ------------------------------------------------------ |
| GET    | `/pokemon/{name}`                 | Standard Pokemon info (name, description, habitat, isLegendary). |
| GET    | `/pokemon/translated/{name}`      | Same shape but with a Yoda or Shakespeare translation. Yoda is applied when the habitat is `cave` or the Pokemon is legendary; Shakespeare otherwise. If translation fails (rate limit, transport error, etc.) the standard description is returned unchanged. |

Successful responses are `200`. Unknown names return `404 { "error": "..." }`. Bad input
returns `400`. Inbound rate-limit violations return `429`.

## Running locally

Prerequisites: **.NET 10 SDK** (preview is fine; the projects target `net10.0`).

```bash
dotnet run --project src/Pokedex.Api
```

The API listens on `http://localhost:5212` (see
[launchSettings.json](src/Pokedex.Api/Properties/launchSettings.json)).

### Examples

```bash
http GET http://localhost:5212/pokemon/mewtwo
http GET http://localhost:5212/pokemon/translated/mewtwo   # legendary -> Yoda
http GET http://localhost:5212/pokemon/translated/zubat    # cave habitat -> Yoda
http GET http://localhost:5212/pokemon/translated/pikachu  # default -> Shakespeare
http GET http://localhost:5212/pokemon/notarealpokemon     # 404
```

OpenAPI docs are exposed at `http://localhost:5212/openapi/v1.json` in development.

## Running with Docker

```bash
docker build -t pokedex-api .
docker run --rm -p 8080:8080 pokedex-api
```

The container listens on `http://localhost:8080`.

## Things I'd do differently in production

- **Endpoint integration tests** — `WebApplicationFactory<Program>` to verify routing,
  model binding, the rate-limit middleware, and the `ToHttpResult` mapping end-to-end.
  Service unit tests + client integration tests give good coverage today; endpoint
  integration is a deliberate gap.
- **Distributed cache** — Redis instead of in-process `MemoryCache` so multiple instances
  share state and the service scales horizontally.
- **Observability** — OpenTelemetry tracing + metrics (request rate, upstream
  success/failure counts, cache hit rate), structured JSON logging, and `/healthz`
  liveness/readiness endpoints for K8s.
- **Secrets** — `appsettings.json` is fine for public URLs but any future API key
  belongs in environment variables / a secret manager, never source-controlled config.
- **Contract tests** — periodic checks against the live PokeAPI to flag schema drift in
  the raw `PokemonSpecies` shape we depend on.
- **HTTP message handler analyzer** for tests — TUnit flags `IDisposable` on test classes
  (TUnit0023). The integration test base uses `[After(Test)]` cleanup to satisfy the
  analyzer; if we ever needed shared per-class fixtures we'd reach for `[ClassDataSource]`.

## Tests

```bash
dotnet run --project tests/Pokedex.Core.Tests
```

> The new Microsoft.Testing.Platform replaces VSTest on .NET 10. The TUnit-based test
> project is an `Exe`, so it runs via `dotnet run`, not `dotnet test`.

Two test suites live in the same project:

- **Service unit tests** — `tests/Pokedex.Core.Tests/Services/Pokemon/` — mock
  `IPokeApiClient` / `IFunTranslationsClient` with NSubstitute. Fast, deterministic, no
  network.
- **Integration tests** — `tests/Pokedex.Core.Tests/IntegrationTests/` — hit the live
  PokeAPI and the FunTranslations mirror. Slow and network-dependent. Both share an
  `IntegrationTestFixture` that builds a real `ServiceProvider` with both typed HTTP
  clients and a counting `DelegatingHandler` (so tests can prove the cache prevents a
  second network round-trip).

The FunTranslations mirror caps each client at **5 requests/minute**. The four translation
integration tests issue exactly 5 outbound requests across them — re-running the suite
inside the same window will briefly fail until the rate-limit window resets.

## Configuration

All tunables live in [appsettings.json](src/Pokedex.Api/appsettings.json) under the
`Pokedex` section:

```jsonc
{
  "Pokedex": {
    "PokeApi":          { "BaseUrl": "...", "SuccessTtlMinutes": 360, "NotFoundTtlSeconds": 60 },
    "FunTranslations":  { "BaseUrl": "...", "SuccessTtlHours": 24 },
    "Cache":            { "MaxEntries": 1024 },
    "RateLimit":        { "PermitLimit": 60, "WindowSeconds": 60 }
  }
}
```

## Architecture notes

### Caching

Both upstream HTTP clients consult a single shared `IMemoryCache` before issuing
network calls. Cache entries are sized at `1`, so the global `SizeLimit` directly bounds
the **entry count** (default 1024), not bytes — simple and predictable memory usage.

- `PokeApiClient` — sliding TTL on success (popular Pokemon stay hot, cold ones expire),
  short absolute TTL on 404 to avoid hammering on a typo without masking it for long, no
  caching on transport errors.
- `FunTranslationsClient` — absolute TTL keyed by `(style, SHA-256(text))`. Translations
  are deterministic, the upstream rate limit is harsh, and absolute TTL means we don't
  hold on to translations forever.

### Rate limiting

ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware applies a
per-remote-IP fixed-window limiter (60 requests/minute by default). 429 with no body when
exceeded.

### Result pattern

Service methods return `Result<T>` from `Pokedex.Core.Common.Models` instead of throwing
on expected failures. The thin endpoint adapter
[EndpointResultExtensions.ToHttpResult](src/Pokedex.Api/Extensions/EndpointResultExtensions.cs)
maps service-layer status codes onto HTTP responses (`Success → 200`, `NotFound → 404`,
`InvalidArgument → 400`, etc.).

## Project layout

```
src/
  Pokedex.Api/                   ASP.NET Core minimal API host
    Endpoints/                   Endpoint groups (Endpoints.cs + PokemonEndpoints.cs)
    Extensions/                  ToHttpResult adapter
    Program.cs                   DI wireup, rate limiter, OpenAPI, endpoint registration
  Pokedex.Core/                  Service + client layer
    Common/Models/Result.cs      Result / Result<T>
    Infrastructure/              ConfigurationExtensions + strongly-typed settings
    Services/
      Pokemon/                   IPokemonService + business logic + Pokemon DTO
      PokeApi/                   IPokeApiClient + raw species/flavor/habitat models
      FunTranslations/           IFunTranslationsClient + raw translation envelope
tests/
  Pokedex.Core.Tests/            TUnit + NSubstitute
    Services/Pokemon/            Service-layer unit tests
    IntegrationTests/            Real PokeAPI + FunTranslations mirror tests
      Common/                    Shared fixture + DelegatingHandler
```
