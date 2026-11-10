namespace LivePoll.Application;

public sealed class VoteAcceptanceResult
{
    public required Guid PollId { get; init; }

    public required Guid OptionId { get; init; }

    public required string Username { get; init; }

    public required bool AcceptedForStorage { get; init; }
}