using System.Diagnostics;

namespace LivePoll.Build.Tests;

internal static class RepositoryScripts
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static void RunNodeScript(string scriptName, params string[] arguments)
    {
        string scriptPath = Path.Combine(RepositoryRoot, "scripts", scriptName);
        Assert.True(File.Exists(scriptPath), $"Missing script {scriptPath}.");

        var start = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start node.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"node {scriptName} exited {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }
    }

    internal static int RunNodeScriptExpectingFailure(string scriptName, params string[] arguments)
    {
        string scriptPath = Path.Combine(RepositoryRoot, "scripts", scriptName);
        var start = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start node.");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LivePoll.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the LivePoll repository root.");
    }
}