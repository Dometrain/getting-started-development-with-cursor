using System.Globalization;
using System.Text.Json;

namespace LivePoll.Integration.Load;

internal sealed record LoadSettings(
    int VoterCount,
    int RampUpSeconds,
    int MaxConnectionsPerServer,
    Guid? PollId);

internal sealed record LoadSample(int ElapsedMs, int VotesPerSecond, long Backlog);

internal sealed record LoadError(int Status, string Detail);

internal static class LoadReport
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Write(
        string root,
        LoadSettings settings,
        IReadOnlyList<LoadSample> samples,
        double? p95VoteLatencyMs,
        IReadOnlyList<LoadError> errors)
    {
        var directory = Path.Combine(root, "results");
        Directory.CreateDirectory(directory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"load-test-{stamp}.json");
        var json = JsonSerializer.Serialize(
            new
            {
                settings,
                samples,
                p95VoteLatencyMs,
                errors,
            },
            jsonOptions);
        File.WriteAllText(path, json);
        return path;
    }
}