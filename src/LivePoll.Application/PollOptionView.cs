namespace LivePoll.Application;

public sealed class PollOptionView
{
    public required Guid Id { get; init; }

    public required string Text { get; init; }
}