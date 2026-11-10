namespace LivePoll.Application;

public interface IPollEndTimeWriter
{
    Task SaveEndsAtAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken);
}