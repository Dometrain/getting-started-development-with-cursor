using System.Text;
using System.Text.RegularExpressions;

namespace LivePoll.Integration.Results;

[CollectionDefinition(ResultsGroup.Name)]
public sealed class ResultsGroup : ICollectionFixture<ResultsFixture>
{
    public const string Name = "LivePoll.Integration.Results";
}

public sealed class ResultsFixture : IAsyncLifetime
{
    private static readonly UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan commandTimeout = TimeSpan.FromMinutes(2);
    private static readonly string[] replicas = ["api1", "api2", "worker1", "worker2"];

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
        await EnsureReplicasAsync();
    }

    public Task DisposeAsync() => EnsureReplicasAsync();

    public async Task KillAsync(string service)
    {
        var killed = await Docker.ComposeAsync(Root, commandTimeout, "kill", service);
        if (killed.ExitCode != 0)
        {
            throw new InvalidOperationException(killed.Stderr + killed.Stdout);
        }
    }

    public async Task EnsureReplicasAsync()
    {
        await StartReplicasAsync();
        await ForgetActivePollsAsync();
        using var client = PublicSite.CreateClient(Origin);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deadline = DateTime.UtcNow.AddSeconds(40);
        Exception? last = null;
        while (DateTime.UtcNow < deadline && seen.Count < 2)
        {
            try
            {
                var destination = await PublicSite.WarmReplicaAsync(client, CancellationToken.None);
                if (destination is "api1" or "api2")
                {
                    seen.Add(destination);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                last = exception;
            }

            if (seen.Count < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }

        if (seen.Count < 2)
        {
            throw new TimeoutException("Both API replicas did not accept sign-in.", last);
        }

        var holderDeadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < holderDeadline)
        {
            try
            {
                var holder = await LeaseHolderAsync();
                if (await IsRunningAsync(holder))
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                last = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        throw new TimeoutException("No running worker holds the publisher lease.", last);
    }

    public async Task<string> LeaseHolderAsync()
    {
        var logs = await Docker.ComposeTextAsync(Root, commandTimeout, "logs", "--no-color", "worker1", "worker2");
        string? holder = null;
        foreach (var line in logs.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var acquired = Regex.Match(line, @"Publisher lease acquired on (worker[12])\b");
            if (acquired.Success)
            {
                holder = acquired.Groups[1].Value;
                continue;
            }

            var lost = Regex.Match(line, @"Publisher lease lost on (worker[12])\b");
            if (lost.Success && lost.Groups[1].Value == holder)
            {
                holder = null;
            }
        }

        if (holder is null || !await IsRunningAsync(holder))
        {
            throw new InvalidOperationException("No worker holds the publisher lease.");
        }

        return holder;
    }

    private async Task<bool> IsRunningAsync(string service)
    {
        var id = (await Docker.ComposeTextAsync(Root, commandTimeout, "ps", "-q", "--status", "running", service)).Trim();
        return id.Length > 0;
    }

    private async Task ForgetActivePollsAsync()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "livepoll-forget-active.sh");
        var script = forgetActivePollsScript.Replace("\r", "", StringComparison.Ordinal);
        await File.WriteAllTextAsync(scriptPath, script, utf8);
        var copied = await Docker.ComposeAsync(
            Root,
            commandTimeout,
            "cp",
            scriptPath,
            "api1:/tmp/livepoll-forget-active.sh");
        if (copied.ExitCode != 0)
        {
            throw new InvalidOperationException(copied.Stderr + copied.Stdout);
        }

        var ran = await Docker.ComposeAsync(
            Root,
            commandTimeout,
            "exec",
            "-T",
            "api1",
            "bash",
            "/tmp/livepoll-forget-active.sh");
        if (ran.ExitCode != 0 || !ran.Stdout.Contains("+OK", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Could not clear active polls (exit {ran.ExitCode}).");
        }
    }

    private async Task StartReplicasAsync()
    {
        var started = await Docker.ComposeAsync(Root, commandTimeout, ["start", .. replicas]);
        if (started.ExitCode != 0)
        {
            throw new InvalidOperationException(started.Stderr + started.Stdout);
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

    private const string forgetActivePollsScript = """
        #!/bin/bash
        set -e
        pass="$Ruvio__Password"
        exec 3<>/dev/tcp/ruvio/6379
        {
          printf $'*2\r\n$4\r\nAUTH\r\n'
          printf '$%s\r\n' "${#pass}"
          printf '%s\r\n' "$pass"
        } >&3
        IFS= read -r auth <&3
        printf '%s\n' "$auth"
        printf $'*2\r\n$3\r\nDEL\r\n$12\r\npolls:active\r\n' >&3
        IFS= read -r deleted <&3
        printf '%s\n' "$deleted"
        """;
}