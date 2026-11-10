namespace LivePoll.Application;

public interface IPollRepository
{
    Task SaveAsync(StoredPoll poll, CancellationToken cancellationToken);

    Task<StoredPoll?> GetAsync(Guid pollId, CancellationToken cancellationToken);
}