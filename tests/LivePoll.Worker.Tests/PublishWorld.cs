using System.Text.Json;

using LivePoll.Application;
using LivePoll.Worker;

namespace LivePoll.Worker.Tests;

internal sealed class PublishWorld
{
    public ManualClock Clock { get; } = new(new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero));

    public FakePublishLock Lock { get; } = new();

    public FakeLiveTotals Totals { get; } = new();

    public FakeActivePolls ActivePolls { get; } = new();

    public FakeResults Results { get; }

    public FakeFence Fence { get; } = new();

    public PublishWorld()
    {
        Results = new FakeResults(Clock);
    }

    public SnapshotPublisher CreatePublisher() =>
        new(Lock, Totals, ActivePolls, Results, Fence, Clock);

    public LivePollPicture Picture(
        Guid pollId,
        Guid optionA,
        Guid optionB,
        long votesA,
        long votesB,
        IReadOnlyList<string>? entryIds = null,
        long? serverUnixMilliseconds = null) =>
        new()
        {
            PollId = pollId,
            EndsAt = Clock.UtcNow.AddMinutes(5),
            Options =
            [
                new LiveOptionTotal(optionA, "Cafeteria", votesA),
                new LiveOptionTotal(optionB, "Food truck", votesB),
            ],
            StreamEntryIds = entryIds ?? [],
            RuvioServerUnixMilliseconds = serverUnixMilliseconds ?? Clock.UtcNow.ToUnixTimeMilliseconds(),
        };
}

internal sealed class ManualClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal sealed class FakePublishLock : IPublishLock
{
    private int busy;

    public List<TimeSpan> Leases { get; } = [];

    public long NextToken { get; set; } = 1;

    public FakeHold? Current { get; private set; }

    public Task<IPublishLockHold?> TryAcquireAsync(TimeSpan lease, CancellationToken cancellationToken)
    {
        Leases.Add(lease);
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
        {
            return Task.FromResult<IPublishLockHold?>(null);
        }

        var hold = new FakeHold(this, NextToken);
        Current = hold;
        return Task.FromResult<IPublishLockHold?>(hold);
    }

    internal void Release()
    {
        Current = null;
        Interlocked.Exchange(ref busy, 0);
    }
}

internal sealed class FakeHold(FakePublishLock owner, long token) : IPublishLockHold
{
    public bool IsHeld { get; set; } = true;

    public long FencingToken { get; } = token;

    public ValueTask DisposeAsync()
    {
        owner.Release();
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeLiveTotals : ILiveTotals
{
    public Func<CancellationToken, Task<IReadOnlyList<LivePollPicture>>>? Handler { get; set; }

    public IReadOnlyList<LivePollPicture> Pictures { get; set; } = [];

    public int Reads { get; private set; }

    public Task<IReadOnlyList<LivePollPicture>> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++;
        if (Handler is not null)
        {
            return Handler(cancellationToken);
        }

        return Task.FromResult(Pictures);
    }
}

internal sealed class FakeActivePolls : IActivePollCleaner
{
    public List<ActivePollEntry> Polls { get; } = [];

    public List<DateTimeOffset> RemovedAt { get; } = [];

    public Task RemoveEndedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        RemovedAt.Add(now);
        Polls.RemoveAll(poll => poll.EndsAt <= now);
        return Task.CompletedTask;
    }
}

internal sealed record ActivePollEntry(Guid PollId, DateTimeOffset EndsAt);

internal sealed class FakeResults(ManualClock clock) : IResultsPublisher
{
    public List<PublishedSnapshot> Messages { get; } = [];

    public Exception? Failure { get; set; }

    public Task PublishAsync(string payload, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Messages.Add(new PublishedSnapshot(clock.UtcNow, payload));
        return Task.CompletedTask;
    }
}

internal sealed record PublishedSnapshot(DateTimeOffset At, string Payload);

internal sealed class FakeFence : ISnapshotFence
{
    private readonly Dictionary<Guid, long> newest = [];

    public Task<bool> TryAcceptAsync(Guid pollId, long fencingToken, CancellationToken cancellationToken)
    {
        if (newest.TryGetValue(pollId, out var current) && fencingToken < current)
        {
            return Task.FromResult(false);
        }

        newest[pollId] = fencingToken;
        return Task.FromResult(true);
    }
}

internal static class SnapshotJson
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static SnapshotDto Read(string payload) =>
        JsonSerializer.Deserialize<SnapshotDto>(payload, jsonOptions)
        ?? throw new InvalidOperationException("Snapshot payload was empty.");
}

internal sealed class SnapshotDto
{
    public Guid PollId { get; set; }

    public bool Ended { get; set; }

    public int VotesPerSecond { get; set; }

    public long FencingToken { get; set; }

    public List<OptionDto> Options { get; set; } = [];
}

internal sealed class OptionDto
{
    public Guid OptionId { get; set; }

    public string Text { get; set; } = "";

    public long Votes { get; set; }
}