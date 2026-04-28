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
returns `400`. Inbound rate-limit violations return `429`. Unhandled errors return RFC 7807
ProblemDetails.

## Running locally

Prerequisites: **.NET 10 SDK** or later.

```bash
dotnet run --project src/Pokedex.Api
```

The API listens on `http://localhost:5000` (see
[launchSettings.json](src/Pokedex.Api/Properties/launchSettings.json)).

### Examples

```bash
http GET http://localhost:5000/pokemon/mewtwo
http GET http://localhost:5000/pokemon/translated/mewtwo   # legendary -> Yoda
http GET http://localhost:5000/pokemon/translated/zubat    # cave habitat -> Yoda
http GET http://localhost:5000/pokemon/translated/pikachu  # default -> Shakespeare
http GET http://localhost:5000/pokemon/notarealpokemon     # 404
```

OpenAPI docs are exposed at `http://localhost:5000/openapi/v1.json` in development.

## Running with Docker

```bash
docker build -t pokedex-api .
docker run --rm -p 8080:8080 pokedex-api
```

The container listens on `http://localhost:8080`.

## How I built it

TDD-style, one commit per logical step. `git log --oneline` walks the design:

1. **Scaffolding and project references** — bare projects wired together, packages added.
2. **Service + DTO contracts** — `IPokemonService`, `Pokemon` DTO, stub implementation.
3. **Upstream client contracts and raw models** — `IPokeApiClient`, `IFunTranslationsClient`, `TranslationStyle`, raw response shapes.
4. **`GetBasicAsync` (failing tests → impl)** — guard clause, species fetch, whitespace normalisation, habitat/legendary mapping.
5. **`GetTranslatedAsync` (failing tests → impl)** — translation routing (Yoda/Shakespeare), fallback on any failure.
6. **PokeAPI client + integration tests** — HTTP client, cache, CountingHandler to prove the cache prevents a second network round-trip.
7. **FunTranslations client** — POST with form-encoded body, SHA-256 cache key.
8. **DI extension method** — `AddPokedexCoreServices`, strongly-typed settings.
9. **Endpoint mapping + Result-to-HTTP adapter** — minimal-API handlers, `ToHttpResult` mapping.
10. **Wireup, rate limiter, OpenAPI** — `Program.cs`, per-IP fixed-window limiter.
11. **Dockerfile + README** — multi-stage build, non-root container user, documentation.

## Things I'd do differently in production

- **Endpoint integration tests** — `WebApplicationFactory<Program>` to verify routing,
  model binding, the rate-limit middleware, and the `ToHttpResult` mapping end-to-end.
  Service unit tests + client integration tests give good coverage today; endpoint
  integration is a deliberate gap.
- **Distributed cache** — Redis instead of in-process `MemoryCache` so multiple instances
  share state and the service scales horizontally. The `SingleFlightCache` abstraction
  would also need a distributed coordination primitive (e.g. a Redis-backed lock) to
  prevent stampedes across pods.
- **HTTP resilience** — configure `HttpClient.Timeout` (default is 100 s) and wire
  `Microsoft.Extensions.Http.Resilience`'s standard handler (timeout + retry with jitter +
  circuit breaker) on both typed clients. As-is, a slow upstream blocks a request thread
  for up to 100 s before the `TaskCanceledException` is caught and surfaced as a 500.
- **Observability** — OpenTelemetry tracing + metrics (request rate, upstream
  success/failure counts, cache hit rate), structured JSON logging.
- **Secrets** — `appsettings.json` is fine for public URLs but any future API key
  belongs in environment variables / a secret manager, never source-controlled config.
- **Contract tests** — periodic checks against the live PokeAPI to flag schema drift in
  the raw `PokemonSpecies` shape we depend on (same for the FunTranslations API).
- **Forwarded headers** — `UseForwardedHeaders` is wired and `KnownIPNetworks` is cleared
  for simplicity; in production restrict to trusted proxy CIDRs and populate
  `KnownIPNetworks` / `KnownProxies` explicitly.
- **FunTranslations** — the brief specifies the `funtranslations.mercxry.me` mirror as
  the target endpoint; the official `api.funtranslations.com` free tier is 5 requests/hour,
  unusable for development. A production deployment should swap in a paid
  `api.funtranslations.com` key and update the `BaseUrl` accordingly.


## Tests

```bash
dotnet run --project tests/Pokedex.Core.Tests
```

> The new Microsoft.Testing.Platform replaces VSTest on .NET 10. The TUnit-based test
> project is an `Exe`, so it runs via `dotnet run`, not `dotnet test`.

Two test suites live in the same project:

- **Service unit tests** — `tests/Pokedex.Core.Tests/UnitTests/Services/Pokemon/` — mock
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

All values are validated at startup (`ValidateOnStart`) — a missing or malformed `BaseUrl`
fails immediately with a clear message rather than silently 404-ing on the first request.

[appsettings.Development.json](src/Pokedex.Api/appsettings.Development.json) overrides
cache TTLs (shorter, so reviewers see fresh data) and raises the rate limit to 1000/min
for local iteration.

## Architecture notes

### Caching and stampede prevention

Both upstream HTTP clients share a single `SingleFlightCache` (backed by `IMemoryCache`)
that provides two guarantees:

1. **Bounded memory** — entries are sized at `1`, so the global `SizeLimit` limits by
   entry count (default 1024), not bytes.
2. **Single-flight** — concurrent callers that arrive while a key is cold are queued
   behind a per-key `SemaphoreSlim`; only one upstream fetch fires, then all waiters
   receive the cached result.

Cache-key namespaces live in `SingleFlightCache` as public constants so there is one
authoritative place for all prefix strings.

- `PokeApiClient` — sliding TTL on success (popular Pokemon stay hot, cold ones expire),
  short absolute TTL on 404, no caching on transport errors.
- `FunTranslationsClient` — absolute TTL keyed by `(style, SHA-256(text))`. Translations
  are deterministic; absolute TTL avoids holding stale translations indefinitely.

### Rate limiting

ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware applies a
per-remote-IP fixed-window limiter (60 requests/minute by default, 1000/minute in
Development). `UseForwardedHeaders` runs first so the correct client IP is used when
behind a reverse proxy.

### Result pattern

Service methods return `Result<T>` from `Pokedex.Core.Common.Models` instead of throwing
on expected failures. The thin endpoint adapter
[EndpointResultExtensions.ToHttpResult](src/Pokedex.Api/Extensions/EndpointResultExtensions.cs)
maps service-layer status codes onto HTTP responses (`Success → 200`, `NotFound → 404`,
`InvalidArgument → 400`, `Error → 500 ProblemDetails`).

## Project layout

```
src/
  Pokedex.Api/                   ASP.NET Core minimal API host
    Endpoints/                   PokemonEndpoints.cs
    Extensions/                  ToHttpResult adapter
    Program.cs                   DI wireup, rate limiter, OpenAPI, endpoint registration
  Pokedex.Core/                  Service + client layer
    Common/
      Caching/SingleFlightCache.cs  Stampede-safe cache wrapper + key-prefix constants
      Json/SnakeCaseJson.cs         Shared JsonSerializerOptions (snake_case)
      Models/Result.cs              Result / Result<T>
    Infrastructure/              ConfigurationExtensions + strongly-typed settings
    Services/
      Pokemon/                   IPokemonService + business logic + Pokemon DTO
      PokeApi/                   IPokeApiClient + raw species/flavor/habitat models
      FunTranslations/           IFunTranslationsClient + raw translation envelope
tests/
  Pokedex.Core.Tests/            TUnit + NSubstitute
    UnitTests/Services/Pokemon/  Service-layer unit tests
    IntegrationTests/            Real PokeAPI + FunTranslations mirror tests
      Common/                    Shared fixture + DelegatingHandler
```

## Implementation notes

This project was implemented with AI assistance (Claude Code).

### My approach

I provided the project scaffolding and requirements upfront, then guided Claude through a
TDD-driven implementation, reviewing each step before proceeding to the next.

### Initial constraints I set

- **TDD** — failing tests written before production code, one commit per logical step.
- **Architecture conventions** — endpoint registration, DI extension, separation of concerns, and coding style modelled under my explicit guidance based on an existing codebase I own.
- **Caching** — in-memory, entry-count bounded (`Size = 1` per entry), config-driven TTLs.
- **Rate limiting** — per-IP fixed-window via ASP.NET Core's built-in middleware.
- **Result pattern** — all service methods return `Result<T>` rather than throwing on expected failures.

### Key decisions I made during implementation

- **Integration tests** — After the service unit tests were written I explicitly requested integration tests against the live PokeAPI and FunTranslations mirror, organized under `IntegrationTests/`.
- **Shared test fixture** — I proposed consolidating the repeated per-test HTTP client setup into a single `IntegrationTestFixture : IAsyncDisposable` shared via a base class, eliminating the boilerplate duplication Claude had initially written.
- **IntegrationTestBase** — I created this base class myself and asked Claude to validate the approach and fix a typo / namespace issue.
- **`SingleFlightCache` extraction** — Pulled the stampede-prevention logic (per-key `SemaphoreSlim` + double-checked locking) out of both HTTP clients into a shared `SingleFlightCache` wrapper. Cache-key namespace prefixes (`PokeApiSpeciesPrefix`, `FunTranslationsPrefix`) live there as public constants so there is one authoritative place for all prefix strings. Semaphores are intentionally never removed — the memory overhead is bounded by `MaxEntries` (≈ 88 bytes × 1024 = ~90 KB worst case).
- **Configuration validation at startup** — Replaced `services.Configure<PokedexSettings>` with `AddOptions<PokedexSettings>().Validate(...).ValidateOnStart()` so a misconfigured `BaseUrl` or invalid TTL fails loudly at boot rather than silently misbehaving on the first request.
- **Middleware hardening** — Added `UseForwardedHeaders` (so rate-limiting sees the real client IP behind a proxy), `AddProblemDetails` + `UseExceptionHandler` (RFC 7807 JSON for unhandled exceptions), `UseStatusCodePages` (JSON for bare status codes), and `UseHsts` in non-development environments.