namespace LivePoll.Worker.Voting;

public sealed class VoteStoreProcessor
{
    private readonly IVoteDeliverySource deliveries;
    private readonly IVoteStore votes;
    private readonly IBacklogCounter backlog;

    public VoteStoreProcessor(
        IVoteDeliverySource deliveries,
        IVoteStore votes,
        IBacklogCounter backlog)
    {
        this.deliveries = deliveries;
        this.votes = votes;
        this.backlog = backlog;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var batch = await deliveries.ReadAsync(cancellationToken);
        foreach (var delivery in batch)
        {
            await ProcessOneAsync(delivery, cancellationToken);
        }
    }

    private async Task ProcessOneAsync(VoteDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.DeliveryCount > 3)
        {
            await deliveries.KeepAsideAsync(delivery, cancellationToken);
            await deliveries.AcknowledgeAsync(delivery.EntryId, cancellationToken);
            return;
        }

        try
        {
            var inserted = await votes.TryStoreOnceAsync(
                delivery.PollId,
                delivery.Username,
                delivery.OptionId,
                cancellationToken);
            if (inserted)
            {
                await backlog.DecrementOnceAsync(cancellationToken);
            }
        }
        catch (VoteOptionMissingException)
        {
            await deliveries.KeepAsideAsync(delivery, cancellationToken);
        }

        await deliveries.AcknowledgeAsync(delivery.EntryId, cancellationToken);
    }
}