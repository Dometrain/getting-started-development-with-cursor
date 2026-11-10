namespace LivePoll.Application;

public sealed class PollView
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<PollOptionView> Options { get; init; }

    public required int DurationSeconds { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required bool Ended { get; init; }
}