using System.Diagnostics;

namespace LivePoll.Integration.Results;

internal readonly record struct DockerResult(int ExitCode, string Stdout, string Stderr);

internal static class Docker
{
    public static Task<DockerResult> ComposeAsync(
        string workingDirectory,
        TimeSpan timeout,
        params string[] args)
    {
        var composeArgs = new string[args.Length + 1];
        composeArgs[0] = "compose";
        args.CopyTo(composeArgs, 1);
        return RunAsync(workingDirectory, timeout, composeArgs);
    }

    public static async Task<string> ComposeTextAsync(
        string workingDirectory,
        TimeSpan timeout,
        params string[] args)
    {
        var result = await ComposeAsync(workingDirectory, timeout, args);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Stderr + result.Stdout);
        }

        return result.Stdout;
    }

    public static async Task<DockerResult> RunAsync(
        string workingDirectory,
        TimeSpan timeout,
        params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("docker did not start.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"docker {string.Join(' ', args)} timed out.");
        }

        return new DockerResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
