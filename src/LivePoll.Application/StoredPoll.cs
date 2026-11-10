using LivePoll.Domain;

namespace LivePoll.Application;

public sealed class StoredPoll
{
    public required Poll Aggregate { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }
}