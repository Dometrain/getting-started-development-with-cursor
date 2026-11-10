using LivePoll.Application;

namespace LivePoll.Application.Tests;

public sealed class VoteAcceptanceTests
{
    private readonly MutableClock clock = new(new DateTimeOffset(2026, 11, 7, 17, 0, 0, TimeSpan.Zero));
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionId = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly DateTimeOffset endsAt = new(2026, 11, 7, 17, 10, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accept_FirstVoteForUsername_IsAccepted()
    {
        var fixture = CreateFixture();
        var service = fixture.Service;

        var result = await service.AcceptAsync(pollId, "ada", optionId);

        Assert.Equal(pollId, result.PollId);
        Assert.Equal(optionId, result.OptionId);
        Assert.Equal("ada", result.Username);
        Assert.True(result.AcceptedForStorage);
        Assert.Equal(1, fixture.Marker.AcceptCount);
        Assert.Single(fixture.Queue.Entries);
        Assert.Equal(1, fixture.Metrics.AcceptanceCount);
    }

    [Fact]
    public async Task Accept_RepeatVote_IsRejected()
    {
        var fixture = CreateFixture();
        var service = fixture.Service;

        await service.AcceptAsync(pollId, "ada", optionId);

        var error = await Assert.ThrowsAsync<ApplicationException>(() =>
            service.AcceptAsync(pollId, "ada", optionId));

        Assert.Equal("This username has already voted on this poll.", error.Message);
        Assert.Single(fixture.Queue.Entries);
        Assert.Equal(1, fixture.Metrics.AcceptanceCount);
    }

    [Fact]
    public async Task Accept_OverlappingCallsThroughFakeOneVoteMarker_OnlyOneAcceptance()
    {
        var fixture = CreateFixture(marker: new ContendedOneVoteMarker());
        var service = fixture.Service;

        var first = service.AcceptAsync(pollId, "ada", optionId);
        var second = service.AcceptAsync(pollId, "ada", optionId);
        var results = await Task.WhenAll(
            RecordAsync(first),
            RecordAsync(second));

        Assert.Equal(1, results.Count(r => r.Accepted));
        Assert.Equal(1, results.Count(r => !r.Accepted));
        Assert.Contains(results, r => r.Error?.Message == "This username has already voted on this poll.");
        Assert.Single(fixture.Queue.Entries);
        Assert.Equal(1, fixture.Metrics.AcceptanceCount);
    }

    [Fact]
    public async Task Accept_AfterEndsAt_IsRejectedAndRecordsNoAcceptance()
    {
        var fixture = CreateFixture();
        clock.UtcNow = endsAt;

        var error = await Assert.ThrowsAsync<ApplicationException>(() =>
            fixture.Service.AcceptAsync(pollId, "ada", optionId));

        Assert.Equal("This poll is closed. It was open for 10 minutes.", error.Message);
        Assert.Equal(0, fixture.Marker.AcceptCount);
        Assert.Empty(fixture.Queue.Entries);
        Assert.Equal(0, fixture.Metrics.AcceptanceCount);
    }

    [Fact]
    public async Task Accept_AsWindowCloses_IsStillAcceptedForStorage()
    {
        var fixture = CreateFixture();
        clock.UtcNow = endsAt.AddTicks(-1);

        var result = await fixture.Service.AcceptAsync(pollId, "ada", optionId);
        clock.UtcNow = endsAt.AddMinutes(1);

        Assert.True(result.AcceptedForStorage);
        Assert.Single(fixture.Queue.Entries);
        Assert.Equal(("ada", optionId), (fixture.Queue.Entries[0].Username, fixture.Queue.Entries[0].OptionId));
    }

    [Fact]
    public async Task Accept_SecondCallUsesCachedEndTime()
    {
        var fixture = CreateFixture();

        await fixture.Service.AcceptAsync(pollId, "ada", optionId);
        await fixture.Service.AcceptAsync(pollId, "grace", optionId);

        Assert.Equal(1, fixture.EndTimes.ReadCount);
    }

    [Fact]
    public async Task Accept_BlankUsername_IsRejected()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ApplicationException>(() =>
            fixture.Service.AcceptAsync(pollId, " ", optionId));

        Assert.Equal(0, fixture.Marker.AcceptCount);
        Assert.Empty(fixture.Queue.Entries);
    }

    [Fact]
    public async Task Accept_MissingPoll_IsRejected()
    {
        var fixture = CreateFixture();
        fixture.EndTimes.EndsAtByPoll.Clear();

        await Assert.ThrowsAsync<ApplicationException>(() =>
            fixture.Service.AcceptAsync(pollId, "ada", optionId));

        Assert.Equal(0, fixture.Marker.AcceptCount);
        Assert.Empty(fixture.Queue.Entries);
    }

    private Fixture CreateFixture(FakeOneVoteMarker? marker = null)
    {
        var endTimes = new FakePollEndTimeReader(pollId, endsAt);
        var oneVoteMarker = marker ?? new FakeOneVoteMarker();
        var queue = new FakeVoteQueue();
        var metrics = new FakeAcceptanceMetrics();
        var service = new VoteAcceptanceService(endTimes, oneVoteMarker, queue, metrics, clock);
        return new Fixture(service, endTimes, oneVoteMarker, queue, metrics);
    }

    private static async Task<(bool Accepted, ApplicationException? Error)> RecordAsync(Task<VoteAcceptanceResult> task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (ApplicationException error)
        {
            return (false, error);
        }
    }

    private sealed record Fixture(
        VoteAcceptanceService Service,
        FakePollEndTimeReader EndTimes,
        FakeOneVoteMarker Marker,
        FakeVoteQueue Queue,
        FakeAcceptanceMetrics Metrics);

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakePollEndTimeReader(Guid pollId, DateTimeOffset endsAt) : IPollEndTimeReader
    {
        public Dictionary<Guid, DateTimeOffset> EndsAtByPoll { get; } = new() { [pollId] = endsAt };

        public int ReadCount { get; private set; }

        public Task<DateTimeOffset?> GetEndsAtAsync(Guid id, CancellationToken cancellationToken)
        {
            ReadCount++;
            EndsAtByPoll.TryGetValue(id, out var value);
            return Task.FromResult<DateTimeOffset?>(EndsAtByPoll.ContainsKey(id) ? value : null);
        }
    }

    private class FakeOneVoteMarker : IOneVoteMarker
    {
        private readonly HashSet<string> accepted = new(StringComparer.Ordinal);

        public int AcceptCount => accepted.Count;

        public virtual Task<bool> TryAcceptAsync(Guid pollId, string username, CancellationToken cancellationToken)
        {
            var key = $"{pollId:N}:{username}";
            return Task.FromResult(accepted.Add(key));
        }
    }

    private sealed class ContendedOneVoteMarker : FakeOneVoteMarker
    {
        private readonly object gate = new();
        private int callers;
        private readonly TaskCompletionSource bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<bool> TryAcceptAsync(Guid pollId, string username, CancellationToken cancellationToken)
        {
            var arrived = Interlocked.Increment(ref callers);
            if (arrived == 1)
            {
                await bothArrived.Task.WaitAsync(cancellationToken);
            }
            else
            {
                bothArrived.TrySetResult();
            }

            lock (gate)
            {
                return base.TryAcceptAsync(pollId, username, cancellationToken).GetAwaiter().GetResult();
            }
        }
    }

    private sealed class FakeVoteQueue : IVoteQueue
    {
        public List<(Guid PollId, string Username, Guid OptionId)> Entries { get; } = [];

        public Task EnqueueAsync(Guid pollId, string username, Guid optionId, CancellationToken cancellationToken)
        {
            Entries.Add((pollId, username, optionId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAcceptanceMetrics : IAcceptanceMetrics
    {
        public int AcceptanceCount { get; private set; }

        public Task IncrementOnAcceptanceAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken)
        {
            AcceptanceCount++;
            return Task.CompletedTask;
        }
    }
}
