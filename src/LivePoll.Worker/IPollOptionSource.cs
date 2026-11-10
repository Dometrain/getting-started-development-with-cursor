namespace LivePoll.Worker;

public interface IPollOptionSource
{
    Task<IReadOnlyList<PollOptionLabel>> ListAsync(Guid pollId, CancellationToken cancellationToken);
}