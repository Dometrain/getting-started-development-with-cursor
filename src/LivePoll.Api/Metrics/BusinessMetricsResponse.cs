namespace LivePoll.Api.Metrics;

public sealed record BusinessMetricsResponse(
    int VotesPerSecond,
    long TotalVotes,
    int ActivePolls,
    IReadOnlyList<OptionVoteMetricResponse> VotesPerOption,
    long VotesWaitingForPostgres);

public sealed record OptionVoteMetricResponse(Guid PollId, Guid OptionId, string Text, long Votes);
