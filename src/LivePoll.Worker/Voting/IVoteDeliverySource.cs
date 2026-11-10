namespace LivePoll.Worker.Voting;

public interface IVoteDeliverySource
{
    Task<IReadOnlyList<VoteDelivery>> ReadAsync(CancellationToken cancellationToken);

    Task AcknowledgeAsync(string entryId, CancellationToken cancellationToken);

    Task KeepAsideAsync(VoteDelivery delivery, CancellationToken cancellationToken);
}