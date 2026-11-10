namespace LivePoll.Worker.Voting;

public sealed record VoteDelivery(
    string EntryId,
    Guid PollId,
    string Username,
    Guid OptionId,
    long DeliveryCount);