namespace LivePoll.Infrastructure.Persistence;

public sealed class PollOptionRecord
{
    public Guid Id { get; set; }

    public Guid PollId { get; set; }

    public required string Text { get; set; }

    public PollRecord? Poll { get; set; }
}