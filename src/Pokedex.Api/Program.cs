using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Pokedex.Api.Endpoints;
using Pokedex.Core.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// Pokedex core services
builder.Services.AddPokedexCoreServices(builder.Configuration);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

app.RegisterPokedexApiEndpoints();

app.Run();
