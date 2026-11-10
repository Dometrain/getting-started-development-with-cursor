using LivePoll.Application;

namespace LivePoll.Api.Tests;

public sealed class RecordingPollEndTimeReader : IPollEndTimeReader, IPollEndTimeWriter
{
    private readonly Dictionary<Guid, DateTimeOffset> endsAtByPoll = new();

    public int ReadCount { get; private set; }

    public void SetEndsAt(Guid pollId, DateTimeOffset endsAt) => endsAtByPoll[pollId] = endsAt;

    public Task SaveEndsAtAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
    {
        SetEndsAt(pollId, endsAt);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetEndsAtAsync(Guid pollId, CancellationToken cancellationToken)
    {
        ReadCount++;
        return Task.FromResult(endsAtByPoll.TryGetValue(pollId, out var endsAt) ? endsAt : (DateTimeOffset?)null);
    }
}

public sealed class RecordingActivePollWriter : IActivePollWriter
{
    public Task AddAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public sealed class RecordingOneVoteMarker : IOneVoteMarker
{
    private readonly HashSet<string> accepted = new(StringComparer.Ordinal);

    public int OnceCalls { get; private set; }

    public Task<bool> TryAcceptAsync(Guid pollId, string username, CancellationToken cancellationToken)
    {
        OnceCalls++;
        var key = $"{pollId:N}:{username}";
        return Task.FromResult(accepted.Add(key));
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Queue matches IVoteQueue, the product term for the accepted-vote work stream.")]
public sealed class RecordingVoteQueue : IVoteQueue
{
    public List<(Guid PollId, string Username, Guid OptionId)> Entries { get; } = [];

    public int XaddCalls => Entries.Count;

    public Task EnqueueAsync(Guid pollId, string username, Guid optionId, CancellationToken cancellationToken)
    {
        Entries.Add((pollId, username, optionId));
        return Task.CompletedTask;
    }
}

public sealed class RecordingAcceptanceMetrics : IAcceptanceMetrics
{
    public int CounterUpdates { get; private set; }

    public Task IncrementOnAcceptanceAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken)
    {
        CounterUpdates++;
        return Task.CompletedTask;
    }
}

public sealed class PostgresTouchGuard
{
    private int touches;

    public int Touches => Volatile.Read(ref touches);

    public void Touch() => Interlocked.Increment(ref touches);
}

public sealed class MutableClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}