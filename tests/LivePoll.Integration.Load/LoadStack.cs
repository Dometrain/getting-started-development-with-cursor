using System.Globalization;

namespace LivePoll.Integration.Load;

[CollectionDefinition(LoadGroup.Name)]
public sealed class LoadGroup : ICollectionFixture<LoadFixture>
{
    public const string Name = "LivePoll.Integration.Load";
}

public sealed class LoadFixture : IAsyncLifetime
{
    private static readonly TimeSpan commandTimeout = TimeSpan.FromMinutes(2);

    public string Root { get; private set; } = "";

    public Uri Origin { get; private set; } = new("http://127.0.0.1/");

    public async Task InitializeAsync()
    {
        Root = FindRoot();
        var up = await Docker.ComposeAsync(Root, TimeSpan.FromMinutes(5), "up", "-d", "--wait");
        if (up.ExitCode != 0)
        {
            throw new InvalidOperationException(up.Stderr + up.Stdout);
        }

        var mapped = (await Docker.ComposeTextAsync(Root, commandTimeout, "port", "nginx", "80")).Trim();
        var port = mapped.Split(':')[^1];
        Origin = new Uri($"http://127.0.0.1:{port}/");
        await EnsureWorkersAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task EnsureWorkersAsync()
    {
        _ = await Docker.ComposeAsync(Root, commandTimeout, "unpause", "worker1", "worker2");
        var started = await Docker.ComposeAsync(Root, commandTimeout, "start", "worker1", "worker2");
        if (started.ExitCode != 0)
        {
            throw new InvalidOperationException(started.Stderr + started.Stdout);
        }
    }

    public Task PauseAsync(params string[] services) => RunOkAsync("pause", services);

    public Task UnpauseAsync(params string[] services) => RunOkAsync("unpause", services);

    public Task KillAsync(params string[] services) => RunOkAsync("kill", services);

    public Task StartAsync(params string[] services) => RunOkAsync("start", services);

    public async Task<bool> IsRunningAsync(string service)
    {
        var id = (await Docker.ComposeTextAsync(Root, commandTimeout, "ps", "-q", "--status", "running", service)).Trim();
        return id.Length > 0;
    }

    public async Task<string> EnvironmentValueAsync(string service, string name)
    {
        var id = (await Docker.ComposeTextAsync(Root, commandTimeout, "ps", "-q", service)).Trim();
        var env = await Docker.ComposeTextAsync(Root, commandTimeout, "exec", "-T", service, "printenv", name);
        if (env.Trim().Length == 0)
        {
            throw new InvalidOperationException($"{service} has no {name}. Container {id}.");
        }

        return env.Trim();
    }

    public async Task<bool> CanOpenTcpAsync(string service, string host, int port)
    {
        var probe = $"timeout 2 bash -c 'echo >/dev/tcp/{host}/{port}'";
        var result = await Docker.ComposeAsync(Root, commandTimeout, "exec", "-T", service, "bash", "-c", probe);
        return result.ExitCode == 0;
    }

    public async Task<int> CountVotesAsync(Guid pollId, string? username)
    {
        var filter = username is null
            ? $"\"PollId\" = '{pollId}'"
            : $"\"PollId\" = '{pollId}' and \"UserId\" = '{username.Replace("'", "''", StringComparison.Ordinal)}'";
        var sql = $"select count(*) from votes where {filter}";
        var text = await Docker.ComposeTextAsync(
            Root,
            commandTimeout,
            "exec",
            "-T",
            "postgres",
            "psql",
            "-U",
            "livepoll",
            "-d",
            "livepoll",
            "-tAc",
            sql);
        return int.Parse(text.Trim(), CultureInfo.InvariantCulture);
    }

    public async Task WaitForVotesAsync(Guid pollId, string? username, int expected, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        var count = 0;
        while (DateTime.UtcNow < deadline)
        {
            count = await CountVotesAsync(pollId, username);
            if (count >= expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException($"Poll {pollId} stored {count} of {expected} votes.");
    }

    private async Task RunOkAsync(string command, params string[] services)
    {
        var result = await Docker.ComposeAsync(Root, commandTimeout, [command, .. services]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Stderr + result.Stdout);
        }
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
