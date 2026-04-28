using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Pokedex.Api.Endpoints;
using Pokedex.Core.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// ProblemDetails for structured error responses on unhandled exceptions and bare status codes
builder.Services.AddProblemDetails();

// Pokedex core services
builder.Services.AddPokedexCoreServices(builder.Configuration);

// Honour X-Forwarded-For / X-Forwarded-Proto set by reverse proxies so the per-IP
// rate limiter and HTTPS redirect work correctly in containerised deployments.
// KnownNetworks/KnownProxies are cleared here for simplicity; in production restrict
// these to trusted proxy CIDRs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Per-remote-IP fixed-window rate limiter
var pokedexSettings = builder.Configuration.GetSection("Pokedex").Get<PokedexSettings>()
    ?? throw new InvalidOperationException("Configuration section 'Pokedex' is missing or empty.");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = pokedexSettings.RateLimit.PermitLimit,
                Window = TimeSpan.FromSeconds(pokedexSettings.RateLimit.WindowSeconds),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapPokemonEndpoints();

app.Run();
