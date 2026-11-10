using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LivePoll.Api.Tests;

public sealed class RuvioTestContainer : IAsyncDisposable
{
    public const string Password = "livepoll-test";

    private readonly IContainer container;

    public RuvioTestContainer()
    {
        var image = ReadRuvioImage();
        container = new ContainerBuilder(image)
            .WithEnvironment("RUVIO_REQUIREPASS", Password)
            .WithEnvironment("RUVIO_DURABILITY", "everysec")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(
                6379,
                wait => wait.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();
    }

    public int Port { get; private set; }

    public async Task StartAsync()
    {
        await container.StartAsync();
        Port = container.GetMappedPublicPort(6379);
        await Task.Delay(500);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    private static string ReadRuvioImage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var composePath = Path.Combine(directory.FullName, "docker-compose.yml");
            if (File.Exists(composePath))
            {
                var lines = File.ReadAllLines(composePath);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (!string.Equals(lines[index].TrimEnd(), "  ruvio:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    for (var lookAhead = index + 1; lookAhead < lines.Length; lookAhead++)
                    {
                        var line = lines[lookAhead];
                        if (line.StartsWith("    image:", StringComparison.Ordinal))
                        {
                            return line["    image:".Length..].Trim();
                        }

                        if (line.Length > 0 && line[0] != ' ' && line[0] != '\t')
                        {
                            break;
                        }
                    }
                }

                throw new InvalidOperationException("Ruvio image was not found in docker-compose.yml.");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("docker-compose.yml was not found.");
    }
}
