using LivePoll.Application;

namespace LivePoll.Application.Tests;

public sealed class BusinessMetricsTests
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");
    private readonly DateTimeOffset now = new(2026, 11, 7, 17, 5, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_UsesAcceptedNotStoredCounterAsBacklog()
    {
        var store = new FakeBusinessMetricsStore
        {
            TotalVotes = 6,
            Backlog = 2,
            OptionVotes = { [(pollId, optionA)] = 4, [(pollId, optionB)] = 2 },
            ActiveEndTimes = { now.AddMinutes(5) },
            StreamEntryIds = BuildPreviousSecondIds(now, count: 3),
            RuvioServerUnixMilliseconds = now.ToUnixTimeMilliseconds(),
        };
        var service = new BusinessMetricsService(store, new FixedClock(now));

        var metrics = await service.GetAsync(
            [
                new OptionMetricQuery(pollId, optionA, "Cafeteria"),
                new OptionMetricQuery(pollId, optionB, "Food truck"),
            ]);

        Assert.Equal(3, metrics.VotesPerSecond);
        Assert.Equal(6, metrics.TotalVotes);
        Assert.Equal(1, metrics.ActivePolls);
        Assert.Equal(2, metrics.VotesWaitingForPostgres);
        Assert.Equal(2, metrics.VotesPerOption.Count);
        Assert.Equal(4, metrics.VotesPerOption.Single(o => o.OptionId == optionA).Votes);
        Assert.Equal(2, metrics.VotesPerOption.Single(o => o.OptionId == optionB).Votes);
    }

    [Fact]
    public async Task Get_MissingTotalOptionOrBacklog_ReadsAsZero()
    {
        var store = new FakeBusinessMetricsStore
        {
            TotalVotes = null,
            Backlog = null,
            ActiveEndTimes = { now.AddMinutes(1) },
            StreamEntryIds = [],
            RuvioServerUnixMilliseconds = now.ToUnixTimeMilliseconds(),
        };
        var service = new BusinessMetricsService(store, new FixedClock(now));

        var metrics = await service.GetAsync(
            [new OptionMetricQuery(pollId, optionA, "Cafeteria")]);

        Assert.Equal(0, metrics.TotalVotes);
        Assert.Equal(0, metrics.VotesWaitingForPostgres);
        Assert.Equal(0, metrics.VotesPerOption.Single().Votes);
        Assert.Equal(0, metrics.VotesPerSecond);
        Assert.Equal(1, metrics.ActivePolls);
    }

    private static List<string> BuildPreviousSecondIds(DateTimeOffset ruvioNow, int count)
    {
        var previousSecondStart = ruvioNow.ToUnixTimeMilliseconds() - (ruvioNow.ToUnixTimeMilliseconds() % 1000) - 1000;
        return Enumerable.Range(0, count).Select(i => $"{previousSecondStart + i}-0").ToList();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeBusinessMetricsStore : IBusinessMetricsStore
    {
        public long? TotalVotes { get; init; }

        public long? Backlog { get; init; }

        public Dictionary<(Guid PollId, Guid OptionId), long?> OptionVotes { get; } = new();

        public List<DateTimeOffset> ActiveEndTimes { get; } = [];

        public IReadOnlyList<string> StreamEntryIds { get; init; } = [];

        public long RuvioServerUnixMilliseconds { get; init; }

        public Task<long?> GetTotalVotesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(TotalVotes);

        public Task<long?> GetOptionVotesAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken)
        {
            OptionVotes.TryGetValue((pollId, optionId), out var votes);
            return Task.FromResult(votes);
        }

        public Task<long?> GetAcceptedNotStoredBacklogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Backlog);

        public Task<IReadOnlyList<DateTimeOffset>> GetActivePollEndTimesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DateTimeOffset>>(ActiveEndTimes);

        public Task<(IReadOnlyList<string> EntryIds, long RuvioServerUnixMilliseconds)> GetVoteStreamEntryIdsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult((StreamEntryIds, RuvioServerUnixMilliseconds));
    }
}