namespace LivePoll.Application;

public sealed class BusinessMetricsView
{
    public required int VotesPerSecond { get; init; }

    public required long TotalVotes { get; init; }

    public required int ActivePolls { get; init; }

    public required IReadOnlyList<OptionVoteMetricView> VotesPerOption { get; init; }

    public required long VotesWaitingForPostgres { get; init; }
}