namespace LivePoll.Application;

public interface IActivePollWriter
{
    Task AddAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken);
}