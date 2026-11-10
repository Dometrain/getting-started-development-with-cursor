using LivePoll.Api.Metrics;
using LivePoll.Application;

namespace LivePoll.Api.Tests;

public sealed class ManualBusinessMetricsStore : IBusinessMetricsStore
{
    public long? TotalVotes { get; set; }

    public long? Backlog { get; set; }

    public Dictionary<(Guid PollId, Guid OptionId), long?> OptionVotes { get; } = [];

    public List<DateTimeOffset> ActiveEndTimes { get; } = [];

    public List<string> StreamEntryIds { get; } = [];

    public long RuvioServerUnixMilliseconds { get; set; }

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
        Task.FromResult<IReadOnlyList<DateTimeOffset>>(ActiveEndTimes.ToList());

    public Task<(IReadOnlyList<string> EntryIds, long RuvioServerUnixMilliseconds)> GetVoteStreamEntryIdsAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<string>, long)>((StreamEntryIds.ToList(), RuvioServerUnixMilliseconds));
}

public sealed class ManualActivePollDirectory : IActivePollDirectory
{
    public List<ActivePollEntry> Entries { get; } = [];

    public Task<IReadOnlyList<ActivePollEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActivePollEntry>>(Entries.ToList());
}
