namespace LivePoll.Application;

public sealed class BusinessMetricsService
{
    private readonly IBusinessMetricsStore store;
    private readonly IClock clock;

    public BusinessMetricsService(IBusinessMetricsStore store, IClock clock)
    {
        this.store = store;
        this.clock = clock;
    }

    public async Task<BusinessMetricsView> GetAsync(
        IReadOnlyList<OptionMetricQuery> options,
        CancellationToken cancellationToken = default)
    {
        var totalVotes = await store.GetTotalVotesAsync(cancellationToken) ?? 0;
        var backlog = await store.GetAcceptedNotStoredBacklogAsync(cancellationToken) ?? 0;
        var endTimes = await store.GetActivePollEndTimesAsync(cancellationToken);
        var (entryIds, ruvioServerUnixMilliseconds) = await store.GetVoteStreamEntryIdsAsync(cancellationToken);

        var votesPerOption = new List<OptionVoteMetricView>(options.Count);
        foreach (var option in options)
        {
            var votes = await store.GetOptionVotesAsync(option.PollId, option.OptionId, cancellationToken) ?? 0;
            votesPerOption.Add(new OptionVoteMetricView
            {
                PollId = option.PollId,
                OptionId = option.OptionId,
                Text = option.Text,
                Votes = votes,
            });
        }

        return new BusinessMetricsView
        {
            VotesPerSecond = VotesPerSecond.FromStreamEntryIds(entryIds, ruvioServerUnixMilliseconds),
            TotalVotes = totalVotes,
            ActivePolls = ActivePolls.CountByEndTime(endTimes, clock.UtcNow),
            VotesPerOption = votesPerOption,
            VotesWaitingForPostgres = backlog,
        };
    }
}