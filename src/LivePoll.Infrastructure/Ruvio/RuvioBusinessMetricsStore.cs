using LivePoll.Application;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

public sealed class RuvioBusinessMetricsStore(IRuvioClient client) : IBusinessMetricsStore
{
    public async Task<long?> GetTotalVotesAsync(CancellationToken cancellationToken) =>
        await client.GetInt64Async(RuvioKeys.TotalVotes, cancellationToken);

    public async Task<long?> GetOptionVotesAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken) =>
        await client.GetInt64Async(RuvioKeys.OptionVotes(pollId, optionId), cancellationToken);

    public async Task<long?> GetAcceptedNotStoredBacklogAsync(CancellationToken cancellationToken) =>
        await client.GetInt64Async(RuvioKeys.Backlog, cancellationToken);

    public async Task<IReadOnlyList<DateTimeOffset>> GetActivePollEndTimesAsync(CancellationToken cancellationToken)
    {
        var entries = await client.GetSortedSetRangeByRankWithScoresAsync(
            RuvioKeys.ActivePolls,
            0,
            -1,
            false,
            cancellationToken);
        var endTimes = new List<DateTimeOffset>(entries.Count);
        foreach (var entry in entries)
        {
            endTimes.Add(DateTimeOffset.FromUnixTimeMilliseconds((long)entry.Score));
        }

        return endTimes;
    }

    public async Task<(IReadOnlyList<string> EntryIds, long RuvioServerUnixMilliseconds)> GetVoteStreamEntryIdsAsync(
        CancellationToken cancellationToken)
    {
        var entries = await client.GetStreamRangeAsync(
            RuvioKeys.VoteStream,
            StreamIds.Min,
            StreamIds.Max,
            false,
            cancellationToken);
        var entryIds = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            entryIds.Add(entry.Id);
        }

        var serverUnixMilliseconds = await ReadServerUnixMillisecondsAsync(cancellationToken);
        return (entryIds, serverUnixMilliseconds);
    }

    private async Task<long> ReadServerUnixMillisecondsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entryId = await client.AddStreamEntryAsync(RuvioKeys.ServerClock, StreamIds.New, 1, ("sample", "1"));
        var separator = entryId.IndexOf('-');
        if (separator <= 0 || !long.TryParse(entryId.AsSpan(0, separator), out var milliseconds))
        {
            throw new InvalidOperationException("Ruvio stream id had no server time.");
        }

        return milliseconds;
    }
}
