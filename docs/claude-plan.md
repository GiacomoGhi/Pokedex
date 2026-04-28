# Pokedex REST API – Implementation Plan

## Context

We need to build a small REST API on top of the existing `c:\Sources\Pokedex` scaffolding (already contains `Pokedex.Api`, `Pokedex.Core`, `Pokedex.Core.Tests` with TUnit, plus `Result`/`Result<T>` in `Pokedex.Core.Common.Models`). The API exposes two endpoints:

1. `GET /pokemon/{name}` – basic Pokemon info from PokeAPI (`name`, `description`, `habitat`, `isLegendary`).
2. `GET /pokemon/translated/{name}` – same payload but with a Yoda translation when habitat is `cave` or the Pokemon is legendary, otherwise a Shakespeare translation. Translation failures fall back to the standard description.

The implementation must follow AlgoTradingHub.Web conventions (file-scoped namespaces, primary-constructor services, `Result<T>` returns, the `Endpoints.cs` registration pattern, the `ConfigurationExtensions.cs` DI pattern), use TDD, in-memory caching that respects memory limits, and built-in ASP.NET rate limiting.

## Decisions locked in with the user

- Only `/pokemon-species/{name}` is needed per request (it already returns name, flavor texts, habitat, is_legendary). No fan-out parallelism is strictly required, so `Task.WhenAll` will not be introduced for the current scope; we will document this in the README and leave the service shape so it is trivial to add later.
- Service-layer tests use NSubstitute to mock `IPokeApiClient` and `IFunTranslationsClient` (no HTTP-message-handler stubs at this layer).
- HTTP clients are covered by **integration tests** that hit the live PokeAPI and the FunTranslations mirror, organised under `tests/Pokedex.Core.Tests/IntegrationTests/` and sharing a single `IntegrationTestFixture` that builds a real `ServiceProvider` with both typed `HttpClient` registrations (DI-driven) and `IAsyncDisposable` cleanup.
- API endpoint integration tests (`WebApplicationFactory<Program>`) are intentionally out of scope; the README will note this and what we'd add in production.
- Caching lives inside the upstream HTTP clients (`PokeApiClient`, `FunTranslationsClient`). The service layer stays pure business logic.

## Target architecture

### `Pokedex.Core`

```
Common/Models/Result.cs                              (already exists)
Infrastructure/
    ConfigurationExtensions.cs                        AddPokedexCoreServices(IServiceCollection, IConfiguration)
    PokedexSettings.cs                                Strongly-typed config (BaseUrls, Cache TTLs, cache size limit)
Services/
    Pokemon/
        IPokemonService.cs
        PokemonService.cs
        Models/
            Pokemon.cs                                Service-layer DTO returned in Result<Pokemon>
    PokeApi/
        IPokeApiClient.cs                             GetSpeciesAsync(string name, CancellationToken) -> Result<PokemonSpecies>
        PokeApiClient.cs                              HttpClient + IMemoryCache; sliding TTL ~6 h, size-limited
        Models/
            PokemonSpecies.cs                         Internal raw shape (name, flavor_text_entries, habitat, is_legendary)
            FlavorTextEntry.cs
            NamedApiResource.cs                       { name, url } – reused for habitat
    FunTranslations/
        IFunTranslationsClient.cs                     TranslateAsync(TranslationStyle style, string text, CancellationToken) -> Result<string>
        FunTranslationsClient.cs                      HttpClient + IMemoryCache keyed by (style, text); long TTL ~24 h
        TranslationStyle.cs                           enum { Yoda, Shakespeare }
        Models/
            TranslationResponse.cs                    Internal raw shape (success, contents.translated)
```

### `Pokedex.Api`

```
Endpoints/
    Endpoints.cs                                      RegisterPokedexApiEndpoints – mirrors AlgoTradingHub.Api/Endpoints/Enpoints.cs
    PokemonEndpoints.cs                               MapPokemonEndpoints – two minimal-API handlers
Extensions/
    EndpointResultExtensions.cs                       ToHttpResult(Result) / ToHttpResult<T>(Result<T>) – copy from AlgoTradingHub
Program.cs                                            Wires DI, MemoryCache, RateLimiter, OpenAPI, Endpoints
appsettings.json                                      "Pokedex" section: BaseUrls + Cache + RateLimit
appsettings.Development.json                          dev overrides if needed
```

### `Pokedex.Core.Tests`

```
Services/Pokemon/
    PokemonServiceTests.cs                            Service-layer TDD; mocks IPokeApiClient and IFunTranslationsClient via NSubstitute.
IntegrationTests/
    Common/
        CountingHandler.cs                            DelegatingHandler that counts outbound requests to assert cache hits.
        IntegrationTestFixture.cs                     Real ServiceProvider wiring both typed clients; IAsyncDisposable.
    PokeApi/
        PokeApiClientTests.cs                         Live PokeAPI – mapping, 404, cache, guard clauses.
    FunTranslations/
        FunTranslationsClientTests.cs                 Live FunTranslations mirror – Yoda/Shakespeare smoke, cache, guards.
```

API endpoints are not unit-tested – they are thin pass-throughs (`(await service.X()).ToHttpResult()`). Production would add `WebApplicationFactory<Program>` tests for routing/rate-limit middleware; this is documented in the README.

## Reused references

- `Result` / `Result<T>`: [src/Pokedex.Core/Common/Models/Result.cs](src/Pokedex.Core/Common/Models/Result.cs)
- Endpoints registration shape mirrors: [c:\Sources\AlgoTradingHub.Web\src\backend\AlgoTradingHub.Api\Endpoints\Enpoints.cs](c:/Sources/AlgoTradingHub.Web/src/backend/AlgoTradingHub.Api/Endpoints/Enpoints.cs) and `AuthEndpoints.cs`
- `ToHttpResult()` extension is copied verbatim (or near-verbatim) from [c:\Sources\AlgoTradingHub.Web\src\backend\AlgoTradingHub.Api\Extensions\EndpointResultExtensions.cs](c:/Sources/AlgoTradingHub.Web/src/backend/AlgoTradingHub.Api/Extensions/EndpointResultExtensions.cs)
- `ConfigurationExtensions` shape mirrors: [c:\Sources\AlgoTradingHub.Web\src\backend\AlgoTradingHub.Core\Infrastructure\ConfigurationExtensions.cs](c:/Sources/AlgoTradingHub.Web/src/backend/AlgoTradingHub.Core/Infrastructure/ConfigurationExtensions.cs) (we only need the `AddPokedexCoreServices` method – no DB)
- Coding style rules (file-scoped namespaces, primary-constructor services, `this.` on instance methods, `Result.Success/NotFound/Error` non-generic factories): [c:\Sources\AlgoTradingHub.Web\Docs\CODING_STYLE_GUIDE.md](c:/Sources/AlgoTradingHub.Web/Docs/CODING_STYLE_GUIDE.md)

## Caching strategy

Memory pressure is bounded by:

- A single `IMemoryCache` registered with `MemoryCacheOptions.SizeLimit` (e.g. 1024 entries) wired in `Program.cs`.
- Every cache entry sets `Size = 1` so the limit is in **entry count**, not bytes – simple and predictable.
- `PokeApiClient` uses `SlidingExpiration = 6h` (PokeAPI data is essentially static; sliding lets popular Pokemon stay hot, cold ones expire).
- `FunTranslationsClient` uses `AbsoluteExpiration = 24h` keyed by `(style, normalised text)` – translations are deterministic and the upstream rate limit is harsh (free tier ~5/hour).
- All TTLs and the size limit live in `PokedexSettings` so they are config-driven.

Negative results (404 not-found from PokeAPI) are cached with a short TTL (e.g. 60 s) to avoid hammering the upstream when a bad name is being retried; this is small enough not to mask a typo for long.

## Rate limiting

ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware:

- Fixed-window limiter, default policy: 60 requests per minute per remote IP, queue size 0.
- 429 response with `Retry-After` set automatically.
- Limit values come from `Pokedex:RateLimit` in `appsettings.json`.

This protects us from runaway clients and indirectly keeps our outbound FunTranslations usage bounded (combined with the 24 h cache).

## Parallelism

With only `/pokemon-species/{name}` involved, both endpoints have a single critical-path call (endpoint 2 has two sequential calls: species → translator, sequential by necessity because translator selection depends on species data). No `Task.WhenAll` is introduced. The README will note that if the response shape grew (e.g. add types/sprites from `/pokemon/{name}`), the service is structured so callers can be issued in parallel via `Task.WhenAll`.

---

## TDD execution plan – one commit per step

Each step lists the failing test(s) first, the production code that makes them pass, and any refactor.

### Step 1 – Project plumbing (no behaviour change)

- Add project references: `Pokedex.Api → Pokedex.Core`, `Pokedex.Core.Tests → Pokedex.Core`.
- Add NuGet packages:
  - `Pokedex.Core`: `Microsoft.Extensions.Http`, `Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Options.ConfigurationExtensions`.
  - `Pokedex.Core.Tests`: `NSubstitute`, reference to `Pokedex.Core`.
  - `Pokedex.Api` already pulls `Microsoft.AspNetCore.RateLimiting` via the Web SDK.
- Verify `dotnet build` / `dotnet test` (no tests yet) succeeds.

**Commit:** `chore: wire project references and dependencies`

### Step 2 – Pokemon DTO and service contract

- Add `Pokedex.Core/Services/Pokemon/Models/Pokemon.cs` (class with `Name`, `Description`, `Habitat?`, `IsLegendary`, `{ get; set; } = default!;` per style guide).
- Add `IPokemonService` with `GetBasicAsync(string name, CancellationToken)` and `GetTranslatedAsync(string name, CancellationToken)` returning `Task<Result<Pokemon>>`.
- Add empty `PokemonService` (primary constructor stub) so the test project compiles.
- **No tests yet** – this is just contracts.

**Commit:** `feat(core): pokemon service contract and DTO`

### Step 3 – Upstream client contracts and raw models

- Add `IPokeApiClient` with `GetSpeciesAsync(string name, CancellationToken) -> Task<Result<PokemonSpecies>>`.
- Add `PokemonSpecies`, `FlavorTextEntry`, `NamedApiResource` raw models (internal mapping shape; not the public response).
- Add `IFunTranslationsClient` with `TranslateAsync(TranslationStyle style, string text, CancellationToken) -> Task<Result<string>>` (unwraps the API's nested `contents.translated`; surfaces any failure as `Result.Error`).
- Add `TranslationStyle` enum `{ Yoda, Shakespeare }`.
- Stub implementations throwing `NotImplementedException`.

**Commit:** `feat(core): upstream client contracts (PokeAPI, FunTranslations)`

### Step 4 – PokemonService.GetBasicAsync (TDD)

`PokemonServiceTests` (TUnit + NSubstitute):

1. `GetBasicAsync_WithUnknownName_ReturnsNotFound` – arrange `IPokeApiClient` to return `Result<PokemonSpecies>.NotFound`, expect `ResultStatus.NotFound`.
2. `GetBasicAsync_WithEmptyName_ReturnsInvalidArgument` – guard clause.
3. `GetBasicAsync_MapsSpeciesToPokemonDto` – species with multiple flavor text entries (English + Japanese, embedded `\n`/`\f` newlines), expect first English entry, normalised whitespace, habitat name, `IsLegendary`.
4. `GetBasicAsync_WithNullHabitat_ReturnsNullHabitat` – PokeAPI may return `null` habitat for some species.
5. `GetBasicAsync_WhenClientErrors_PropagatesError`.

Implementation: guard clause, await the client, map species → `Pokemon`, return `Result.Success(pokemon)` (non-generic factory per style guide).

**Commit:** `feat(core): GetBasicAsync with TDD`

### Step 5 – PokemonService.GetTranslatedAsync (TDD)

Tests added to the same `PokemonServiceTests`:

1. `GetTranslatedAsync_WithCaveHabitat_UsesYoda` – translator client called with `Yoda`, response uses translated text.
2. `GetTranslatedAsync_WithLegendary_UsesYoda` – even when habitat ≠ cave.
3. `GetTranslatedAsync_OtherwiseUsesShakespeare`.
4. `GetTranslatedAsync_WhenTranslationFails_FallsBackToStandardDescription` – translator returns `Result.Error`, response keeps the species's flavor text.
5. `GetTranslatedAsync_WhenSpeciesNotFound_ReturnsNotFoundWithoutCallingTranslator` – verify `IFunTranslationsClient` received `0` calls.
6. `GetTranslatedAsync_WithEmptyName_ReturnsInvalidArgument`.

Implementation:

- Reuse `GetBasicAsync` for the species fetch path (private helper or call into a shared mapper).
- Choose translator: `species.IsLegendary || species.Habitat?.Name == "cave" ? Yoda : Shakespeare`.
- On translator success → swap description, on failure → keep original. Always return the same `Pokemon` shape.

**Commit:** `feat(core): GetTranslatedAsync with TDD`

### Step 6 – PokeApiClient implementation + integration tests

Implementation:

- `PokeApiClient` uses `HttpClient` (registered via typed `AddHttpClient<IPokeApiClient, PokeApiClient>`).
- Reads `BaseUrl` from `PokedexSettings`.
- Wraps `GetAsync` + `ReadFromJsonAsync<PokemonSpecies>` (snake_case naming policy); maps `404 → Result.NotFound`, transport / JSON errors → `Result.Error`.
- Inserts `IMemoryCache` lookup before the HTTP call; cache key `pokeapi:species:{loweredName}`. Sliding 6 h on success, absolute 60 s on 404, no caching on transport errors. Cache entries are sized at `1` so the global `SizeLimit` bounds memory by entry count.

Integration tests (real network — added on user request):

- New folder `tests/Pokedex.Core.Tests/IntegrationTests/`.
- Shared `Common/CountingHandler.cs` – `DelegatingHandler` that counts every outbound request before forwarding to the real `HttpClientHandler`. Lets tests assert "second call did not hit the network" without inspecting `MemoryCache` internals.
- `IntegrationTests/PokeApi/PokeApiClientTests.cs` – verifies legendary mapping, 404, success cache, NotFound cache, empty-name guard.
- TUnit's `[After(Test)]` lifecycle hook is used for cleanup (TUnit0023 analyzer flags the alternative `IDisposable` pattern).

### Step 6.5 – Shared integration test fixture

The user requested a single shared fixture so the two integration test classes stop duplicating the wiring (`HttpClient` + `CountingHandler` + `MemoryCache` + `IOptions<PokedexSettings>` + client construction).

Approach: build a real `ServiceProvider` with `Microsoft.Extensions.DependencyInjection`, register both typed HTTP clients (`AddHttpClient<IPokeApiClient, PokeApiClient>`, `AddHttpClient<IFunTranslationsClient, FunTranslationsClient>`) and inject the per-test `CountingHandler` instances via `ConfigurePrimaryHttpMessageHandler`. The fixture is constructed once per test (so each test still gets fresh handler counts and an empty cache), and `IAsyncDisposable` flushes the provider on teardown.

New file – `tests/Pokedex.Core.Tests/IntegrationTests/Common/IntegrationTestFixture.cs`:

- `internal class IntegrationTestFixture : IAsyncDisposable`.
- Public properties: `IPokeApiClient PokeApiClient`, `IFunTranslationsClient FunTranslationsClient`, `CountingHandler PokeApiHandler`, `CountingHandler FunTranslationsHandler`.
- Constructor:
  - `var services = new ServiceCollection();`
  - `services.AddMemoryCache(o => o.SizeLimit = 100);` – single cache shared by both clients (mirrors production wiring; cache keys are namespaced so there's no collision).
  - `services.Configure<PokedexSettings>(...)` with both `PokeApi` and `FunTranslations` sub-objects pointing at the live PokeAPI and the FunTranslations mirror, and short test-scoped TTLs.
  - `services.AddHttpClient<IPokeApiClient, PokeApiClient>(c => c.BaseAddress = new Uri(...)).ConfigurePrimaryHttpMessageHandler(() => this.PokeApiHandler);`
  - Same for `IFunTranslationsClient` with `this.FunTranslationsHandler`.
  - `_serviceProvider = services.BuildServiceProvider();` and resolve both client interfaces into the public properties.
- `DisposeAsync` calls `await _serviceProvider.DisposeAsync()` (the provider disposes the typed clients, the HttpMessageHandlerFactory handlers, and the MemoryCache).

Refactor of the two test classes:

- Drop the per-class boilerplate (handler / httpClient / cache / settings / client field initialisation).
- Hold `private readonly IntegrationTestFixture _fixture = new();`
- `[After(Test)] public async Task Cleanup() => await _fixture.DisposeAsync();`
- Tests use `_fixture.PokeApiClient` / `_fixture.PokeApiHandler` (or the FunTranslations counterparts).

Verification: `dotnet run --project tests/Pokedex.Core.Tests` – the same test counts and pass/fail status as before the refactor (PokeAPI integration tests green, FunTranslations integration tests RED until step 7).

### Step 7 – FunTranslationsClient implementation (uses the existing fixture)

- Typed `AddHttpClient<IFunTranslationsClient, FunTranslationsClient>` with the mirror base URL `https://funtranslations.mercxry.me/` (already wired in the integration fixture; production wiring lands in step 8).
- POST `translate/{style}.json` with form-encoded `text=...`, parse `contents.translated`.
- Guard clause on empty/whitespace `text` → `Result.InvalidArgument`.
- Any non-2xx (including 429) → `Result.Error("translation unavailable")` so the service layer falls back gracefully.
- `IMemoryCache` keyed by `funtranslations:{style}:{sha256(text)}`, absolute TTL from `FunTranslationsSettings.SuccessTtlHours`. Errors are not cached so transient failures recover on retry.
- Brings the 6 RED FunTranslations integration tests (already in place from step 6.5) to GREEN.

### Step 8 – `Pokedex.Core.Infrastructure.ConfigurationExtensions`

- `AddPokedexCoreServices(IServiceCollection, IConfiguration)`:
  - `services.Configure<PokedexSettings>(configuration.GetSection("Pokedex"))`
  - `services.AddMemoryCache(options => options.SizeLimit = settings.Cache.MaxEntries)`
  - `services.AddHttpClient<IPokeApiClient, PokeApiClient>((sp, client) => client.BaseAddress = ...)`
  - `services.AddHttpClient<IFunTranslationsClient, FunTranslationsClient>(...)`
  - `services.AddScoped<IPokemonService, PokemonService>()`
- One method, mirrors AlgoTradingHub layout. Keeps `Program.cs` short.

### Step 9 – API endpoints + Result extension

- `Pokedex.Api/Extensions/EndpointResultExtensions.cs` – copy of AlgoTradingHub's `ToHttpResult` (no auth-specific bits).
- `Pokedex.Api/Endpoints/PokemonEndpoints.cs` – two static handlers:
  ```csharp
  group.MapGet("/{name}", GetBasicAsync).WithName("GetPokemon");
  group.MapGet("/translated/{name}", GetTranslatedAsync).WithName("GetTranslatedPokemon");
  ```
  Each handler is a one-liner: `(await pokemonService.GetXxxAsync(name, ct)).ToHttpResult()`.
- `Pokedex.Api/Endpoints/Endpoints.cs` – `RegisterPokedexApiEndpoints` group `/pokemon` with `WithTags("Pokemon")`, calls `MapPokemonEndpoints`.

### Step 10 – `Program.cs` wireup + rate limiting

- `builder.Services.AddPokedexCoreServices(builder.Configuration);`
- Configure `AddRateLimiter` with a fixed-window policy bound to `Pokedex:RateLimit` (PermitLimit, Window).
- `app.UseRateLimiter();` before mapping endpoints.
- `app.RegisterPokedexApiEndpoints();`
- Keep `MapOpenApi()` in dev.

### Step 11 – README + Dockerfile + appsettings polish

- `appsettings.json`: full `Pokedex` section (BaseUrls, Cache, RateLimit).
- `Dockerfile` (multi-stage, `mcr.microsoft.com/dotnet/sdk:10.0` build, `aspnet:10.0` runtime, exposes 8080).
- `README.md`:
  - How to run locally (`dotnet run --project src/Pokedex.Api`) and via Docker (`docker build`, `docker run -p 8080:8080`).
  - Example httpie calls per the spec.
  - "Things I'd do differently in production" section: WebApplicationFactory integration tests, contract tests for upstream APIs, distributed cache (Redis) instead of in-process, Polly retries with circuit breakers around upstream calls, structured logging + OpenTelemetry, secrets via env not appsettings, K8s `liveness`/`readiness` probes.

**Commit:** `docs: README, Dockerfile and runtime configuration`

---

## Verification

After Step 5 (services TDD complete) and Step 10 (wiring complete):

1. `dotnet test` – all unit tests in `Pokedex.Core.Tests` green.
2. `dotnet run --project src/Pokedex.Api` then:
   - `http GET http://localhost:5000/pokemon/mewtwo` → 200 with `{ name, description, habitat: "rare", isLegendary: true }`.
   - `http GET http://localhost:5000/pokemon/translated/mewtwo` → 200 with Yoda-translated description (or original if FunTranslations is rate-limited).
   - `http GET http://localhost:5000/pokemon/translated/diglett` → 200 with Yoda translation (cave habitat).
   - `http GET http://localhost:5000/pokemon/translated/pikachu` → 200 with Shakespeare translation.
   - `http GET http://localhost:5000/pokemon/notarealpokemon` → 404 with `{ error: "..." }`.
3. Hammer the endpoint past the configured limit (e.g. 70 quick calls when limit is 60/min) and observe 429s with `Retry-After`.
4. Re-issue the same name twice and observe (via logs/breakpoint) that only the first call hits PokeAPI – cache works.

## Files to be created / modified

- `src/Pokedex.Core/Pokedex.Core.csproj` – packages.
- `src/Pokedex.Core/Common/Models/Result.cs` – unchanged.
- `src/Pokedex.Core/Infrastructure/ConfigurationExtensions.cs` – new.
- `src/Pokedex.Core/Infrastructure/PokedexSettings.cs` – new.
- `src/Pokedex.Core/Services/Pokemon/{IPokemonService,PokemonService}.cs` – new.
- `src/Pokedex.Core/Services/Pokemon/Models/Pokemon.cs` – new.
- `src/Pokedex.Core/Services/PokeApi/{IPokeApiClient,PokeApiClient}.cs` – new.
- `src/Pokedex.Core/Services/PokeApi/Models/{PokemonSpecies,FlavorTextEntry,NamedApiResource}.cs` – new.
- `src/Pokedex.Core/Services/FunTranslations/{IFunTranslationsClient,FunTranslationsClient,TranslationStyle}.cs` – new.
- `src/Pokedex.Core/Services/FunTranslations/Models/TranslationResponse.cs` – new.
- `src/Pokedex.Api/Pokedex.Api.csproj` – add Core ref.
- `src/Pokedex.Api/Extensions/EndpointResultExtensions.cs` – new.
- `src/Pokedex.Api/Endpoints/{Endpoints,PokemonEndpoints}.cs` – new.
- `src/Pokedex.Api/Program.cs` – DI + rate limiter + endpoint registration.
- `src/Pokedex.Api/appsettings.json` – add `Pokedex` section.
- `tests/Pokedex.Core.Tests/Pokedex.Core.Tests.csproj` – add NSubstitute + Core ref.
- `tests/Pokedex.Core.Tests/Services/Pokemon/PokemonServiceTests.cs` – new.
- `tests/Pokedex.Core.Tests/IntegrationTests/Common/CountingHandler.cs` – new.
- `tests/Pokedex.Core.Tests/IntegrationTests/Common/IntegrationTestFixture.cs` – new (step 6.5).
- `tests/Pokedex.Core.Tests/IntegrationTests/PokeApi/PokeApiClientTests.cs` – new, refactored in step 6.5 to use the fixture.
- `tests/Pokedex.Core.Tests/IntegrationTests/FunTranslations/FunTranslationsClientTests.cs` – new, refactored in step 6.5 to use the fixture.
- `Dockerfile` – new.
- `README.md` – overwrite empty file with run/design notes.
