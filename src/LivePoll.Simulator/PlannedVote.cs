namespace LivePoll.Simulator;

public sealed record PlannedVote(
    int Index,
    TimeSpan JoinAfter,
    string Username,
    Guid OptionId);