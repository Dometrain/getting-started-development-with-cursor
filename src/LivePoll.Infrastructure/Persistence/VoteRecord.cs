namespace LivePoll.Infrastructure.Persistence;

public sealed class VoteRecord
{
    public Guid Id { get; set; }

    public Guid PollId { get; set; }

    public Guid OptionId { get; set; }

    public required string UserId { get; set; }

    public DateTimeOffset CastAt { get; set; }

    public PollRecord? Poll { get; set; }

    public PollOptionRecord? Option { get; set; }
}