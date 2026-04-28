namespace Pokedex.Core.Tests.IntegrationTests;

/// <summary>
/// Base class for API integration tests. Provides a fresh
/// <see cref="IntegrationTestFixture"/> per test and disposes it on teardown.
/// </summary>
public class IntegrationTestBase
{
    protected readonly IntegrationTestFixture _fixture = new();

    /// <summary>
    /// Disposes the per-test DI container after each test runs.
    /// </summary>
    [After(Test)]
    public async Task Cleanup() => await _fixture.DisposeAsync();
}
