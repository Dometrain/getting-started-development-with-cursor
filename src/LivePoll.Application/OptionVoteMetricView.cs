namespace LivePoll.Application;

public sealed class OptionVoteMetricView
{
    public required Guid PollId { get; init; }

    public required Guid OptionId { get; init; }

    public required string Text { get; init; }

    public required long Votes { get; init; }
}