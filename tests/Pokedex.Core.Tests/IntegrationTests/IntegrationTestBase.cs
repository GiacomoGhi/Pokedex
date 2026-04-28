namespace Pokedex.Core.Tests.IntegrationTests;

/// <summary>
/// Base class for API integration tests. Provides a fresh
/// <see cref="IntegrationTestFixture"/> per test and disposes it on teardown.
/// </summary>
public class IntegrationTestBase
{
    protected IntegrationTestFixture _fixture = null!;

    [Before(Test)]
    public Task Setup()
    {
        _fixture = new IntegrationTestFixture();
        return Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup() => await _fixture.DisposeAsync();
}
