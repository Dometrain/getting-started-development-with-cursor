using Ruvio.Client;

namespace LivePoll.Infrastructure.Tests;

[CollectionDefinition(Name)]
public sealed class RuvioContainerGroup : ICollectionFixture<RuvioFixture>
{
    public const string Name = "Ruvio";
}

public sealed class RuvioFixture : IAsyncLifetime, IAsyncDisposable
{
    private readonly RuvioContainerSession session = new();

    public IRuvioClient Client => session.Client;

    public int Port => session.Port;

    public string ContainerId => session.ContainerId;

    public Task InitializeAsync() => session.StartAsync();

    public async Task ResetAsync()
    {
        await Client.FlushDatabaseAsync();
    }

    public ValueTask DisposeAsync() => session.DisposeAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }
}
