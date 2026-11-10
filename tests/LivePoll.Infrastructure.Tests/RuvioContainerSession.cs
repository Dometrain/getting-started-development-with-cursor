using System.Diagnostics;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Tests;

public sealed class RuvioContainerSession : IAsyncDisposable
{
    public const string Password = "livepoll-test";

    public static readonly TimeSpan EverysecSyncWait = TimeSpan.FromMilliseconds(2500);

    public static string Image { get; } = DockerComposeImages.Ruvio;

    private IContainer? container;

    private RuvioConnection? connection;

    public IRuvioClient Client { get; private set; } = null!;

    public int Port { get; private set; }

    public string ContainerId => container?.Id ?? throw new InvalidOperationException("Ruvio container is not started.");

    public async Task StartAsync()
    {
        container = CreateContainer();
        await container.StartAsync();
        Port = container.GetMappedPublicPort(6379);
        await ConnectAsync();
    }

    public async Task RestartAsync()
    {
        if (container is null)
        {
            throw new InvalidOperationException("Ruvio container is not started.");
        }

        await DisposeConnectionAsync();
        await container.StopAsync();
        await container.StartAsync();
        Port = container.GetMappedPublicPort(6379);
        await ConnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    public static async Task<string> InspectAsync(string containerId, string format)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"inspect --format \"{format}\" {containerId}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("docker inspect did not start.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(stderr);
        }

        return stdout;
    }

    private async Task ConnectAsync()
    {
        const int attempts = 20;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            RuvioConnection? opened = null;
            try
            {
                opened = await RuvioConnection.ConnectAsync(new RuvioClientOptions
                {
                    Host = "127.0.0.1",
                    Port = Port,
                    Password = Password,
                });
                var client = await opened.GetDefaultDatabaseAsync();
                await client.PingAsync();
                connection = opened;
                Client = client;
                return;
            }
            catch (Exception) when (attempt < attempts)
            {
                if (opened is not null)
                {
                    await opened.DisposeAsync();
                }

                await Task.Delay(250);
            }
        }

        throw new InvalidOperationException("Ruvio did not accept the password.");
    }

    private async Task DisposeConnectionAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }
    }

    private static IContainer CreateContainer() =>
        new ContainerBuilder(Image)
            .WithEnvironment("RUVIO_REQUIREPASS", Password)
            .WithEnvironment("RUVIO_DURABILITY", "everysec")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(
                6379,
                wait =>
                {
                    wait.WithTimeout(TimeSpan.FromMinutes(3));
                }))
            .Build();
}
