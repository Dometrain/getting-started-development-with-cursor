namespace LivePoll.Application;

public interface IPollEndTimeReader
{
    Task<DateTimeOffset?> GetEndsAtAsync(Guid pollId, CancellationToken cancellationToken);
}