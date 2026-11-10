namespace LivePoll.Application;

public interface IBusinessMetricsStore
{
    Task<long?> GetTotalVotesAsync(CancellationToken cancellationToken);

    Task<long?> GetOptionVotesAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken);

    Task<long?> GetAcceptedNotStoredBacklogAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTimeOffset>> GetActivePollEndTimesAsync(CancellationToken cancellationToken);

    Task<(IReadOnlyList<string> EntryIds, long RuvioServerUnixMilliseconds)> GetVoteStreamEntryIdsAsync(
        CancellationToken cancellationToken);
}