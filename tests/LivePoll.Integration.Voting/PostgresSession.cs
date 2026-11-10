using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LivePoll.Integration.Voting;

internal sealed class PostgresSession : IAsyncDisposable
{
    public const string User = "livepoll";

    public const string Password = "livepoll";

    public const string Database = "livepoll";

    private IContainer? container;

    public string ConnectionString { get; private set; } = "";

    public string ContainerId => container?.Id ?? throw new InvalidOperationException("Postgres container is not started.");

    public async Task StartAsync()
    {
        container = new ContainerBuilder(ComposeImages.Postgres)
            .WithEnvironment("POSTGRES_USER", User)
            .WithEnvironment("POSTGRES_PASSWORD", Password)
            .WithEnvironment("POSTGRES_DB", Database)
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(
                5432,
                wait =>
                {
                    wait.WithTimeout(TimeSpan.FromMinutes(3));
                }))
            .Build();
        await container.StartAsync();
        var port = container.GetMappedPublicPort(5432);
        ConnectionString =
            $"Host=127.0.0.1;Port={port};Database={Database};Username={User};Password={Password}";
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}
