namespace LivePoll.Worker;

public sealed class LivePollPicture
{
    public required Guid PollId { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required IReadOnlyList<LiveOptionTotal> Options { get; init; }

    public required IReadOnlyList<string> StreamEntryIds { get; init; }

    public required long RuvioServerUnixMilliseconds { get; init; }
}

public sealed record LiveOptionTotal(Guid OptionId, string Text, long Votes);

public sealed record PollOptionLabel(Guid OptionId, string Text);