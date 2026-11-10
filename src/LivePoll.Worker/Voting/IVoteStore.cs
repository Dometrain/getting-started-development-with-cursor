namespace LivePoll.Worker.Voting;

public interface IVoteStore
{
    Task<bool> TryStoreOnceAsync(
        Guid pollId,
        string username,
        Guid optionId,
        CancellationToken cancellationToken);
}