namespace LivePoll.Infrastructure.Persistence;

public sealed class PollRecord
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public List<PollOptionRecord> Options { get; set; } = [];

    public List<VoteRecord> Votes { get; set; } = [];
}