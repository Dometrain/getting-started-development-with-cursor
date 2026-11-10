namespace LivePoll.Worker;

public interface ISnapshotFence
{
    Task<bool> TryAcceptAsync(Guid pollId, long fencingToken, CancellationToken cancellationToken);
}