namespace Pokedex.Core.Tests.IntegrationTests.Common;

/// <summary>
/// <see cref="DelegatingHandler"/> that counts every outbound HTTP request before
/// forwarding it to the inner real handler. Lets integration tests assert that the
/// in-memory cache prevents a second network round-trip.
/// </summary>
public class CountingHandler : DelegatingHandler
{
    public int RequestCount { get; private set; }

    public CountingHandler()
        : base(new HttpClientHandler())
    {
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.RequestCount++;
        return base.SendAsync(request, cancellationToken);
    }
}
