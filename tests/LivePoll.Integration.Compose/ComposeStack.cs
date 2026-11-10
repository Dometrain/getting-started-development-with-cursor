using System.Text.Json;

namespace LivePoll.Integration.Compose;

internal sealed record ComposeContainer(
    string Id,
    string Service,
    string State,
    IReadOnlyList<string> HostPorts);

[CollectionDefinition(ComposeGroup.Name)]
public sealed class ComposeGroup : ICollectionFixture<ComposeFixture>
{
    public const string Name = "LivePoll.Integration.Compose";
}

public sealed class ComposeFixture : IAsyncLifetime
{
    private static readonly TimeSpan upTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan commandTimeout = TimeSpan.FromMinutes(2);

    public string Root { get; private set; } = "";

    public Uri Origin { get; private set; } = new("http://127.0.0.1/");

    public async Task InitializeAsync()
    {
        Root = FindRoot();
        var up = await DockerCli.ComposeAsync(
            Root,
            upTimeout,
            "up",
            "-d",
            "--build",
            "--wait",
            "--remove-orphans");
        if (up.ExitCode != 0)
        {
            throw new InvalidOperationException(up.Stderr + up.Stdout);
        }

        var tools = await DockerCli.ComposeAsync(
            Root,
            commandTimeout,
            "--profile",
            "tools",
            "rm",
            "-sf",
            "ruvio-ui");
        if (tools.ExitCode != 0)
        {
            throw new InvalidOperationException(tools.Stderr + tools.Stdout);
        }

        var mapped = (await DockerCli.ComposeTextAsync(Root, commandTimeout, "port", "nginx", "80")).Trim();
        var port = mapped.Split(':')[^1];
        Origin = new Uri($"http://127.0.0.1:{port}/");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    internal Task<string> ComposeTextAsync(params string[] args) =>
        DockerCli.ComposeTextAsync(Root, commandTimeout, args);

    internal async Task<IReadOnlyList<ComposeContainer>> ContainersAsync()
    {
        var ids = (await ComposeTextAsync("ps", "-q"))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var containers = new List<ComposeContainer>(ids.Length);
        foreach (var id in ids)
        {
            var service = (await InspectAsync(id, "{{index .Config.Labels \"com.docker.compose.service\"}}")).Trim();
            var state = (await InspectAsync(id, "{{.State.Status}}")).Trim();
            var portsJson = await InspectAsync(id, "{{json .NetworkSettings.Ports}}");
            containers.Add(new ComposeContainer(id, service, state, ReadHostPorts(portsJson)));
        }

        return containers;
    }

    internal async Task<string> InspectAsync(string containerId, string format)
    {
        var result = await DockerCli.RunAsync(
            Root,
            commandTimeout,
            "inspect",
            "--format",
            format,
            containerId);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Stderr);
        }

        return result.Stdout;
    }

    internal async Task<string> EnvironmentValueAsync(string service, string name)
    {
        var id = (await ComposeTextAsync("ps", "-q", service)).Trim();
        var env = await InspectAsync(id, "{{range .Config.Env}}{{println .}}{{end}}");
        var prefix = name + "=";
        var line = env.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Single(entry => entry.StartsWith(prefix, StringComparison.Ordinal));
        return line[prefix.Length..];
    }

    internal async Task<DockerResult> RunAsync(params string[] args) =>
        await DockerCli.RunAsync(Root, commandTimeout, args);

    private static List<string> ReadHostPorts(string json)
    {
        using var document = JsonDocument.Parse(json);
        var ports = new List<string>();
        foreach (var binding in document.RootElement.EnumerateObject())
        {
            if (binding.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in binding.Value.EnumerateArray())
            {
                if (entry.TryGetProperty("HostPort", out var hostPort)
                    && hostPort.GetString() is { Length: > 0 } published)
                {
                    ports.Add(published);
                }
            }
        }

        return ports;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("docker-compose.yml was not found.");
    }
}