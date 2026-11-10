using System.Diagnostics.CodeAnalysis;

namespace LivePoll.Api.Tests;

[CollectionDefinition(Name)]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit collection fixture naming.")]
public sealed class RuvioApiCollection : ICollectionFixture<RuvioApiFixture>
{
    public const string Name = "ruvio-api";
}

public sealed class RuvioApiFixture : IAsyncLifetime, IDisposable
{
    private readonly RuvioTestContainer session = new();

    public int Port => session.Port;

    public static string Password => RuvioTestContainer.Password;

    public async Task InitializeAsync() => await session.StartAsync();

    public async Task DisposeAsync() => await session.DisposeAsync();

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();
}
