using LivePoll.Application;

namespace LivePoll.Api.LiveResults;

public sealed class CurrentResultsReader(
    IPollRepository polls,
    IBusinessMetricsStore metrics,
    IClock clock)
{
    public async Task<ResultsSnapshotResponse?> ReadAsync(Guid pollId, CancellationToken cancellationToken)
    {
        var stored = await polls.GetAsync(pollId, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var options = new List<OptionTotalResponse>(stored.Aggregate.Options.Count);
        foreach (var option in stored.Aggregate.Options)
        {
            var votes = await metrics.GetOptionVotesAsync(pollId, option.Id, cancellationToken) ?? 0;
            options.Add(new OptionTotalResponse(option.Id, option.Text, votes));
        }

        var (entryIds, serverUnixMilliseconds) = await metrics.GetVoteStreamEntryIdsAsync(cancellationToken);
        return new ResultsSnapshotResponse(
            pollId,
            clock.UtcNow >= stored.EndsAt,
            VotesPerSecond.FromStreamEntryIds(entryIds, serverUnixMilliseconds),
            options);
    }
}
