namespace LivePoll.Api.LiveResults;

public sealed record ResultsSnapshotResponse(
    Guid PollId,
    bool Ended,
    int VotesPerSecond,
    IReadOnlyList<OptionTotalResponse> Options);

public sealed record OptionTotalResponse(Guid OptionId, string Text, long Votes);

public sealed record ParsedResultsSnapshot(
    Guid PollId,
    bool Ended,
    int VotesPerSecond,
    long FencingToken,
    IReadOnlyList<OptionTotalResponse> Options)
{
    public ResultsSnapshotResponse ToResponse() =>
        new(PollId, Ended, VotesPerSecond, Options);
}
