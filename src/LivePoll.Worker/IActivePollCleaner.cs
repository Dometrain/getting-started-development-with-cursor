namespace LivePoll.Worker;

public interface IActivePollCleaner
{
    Task RemoveEndedAsync(DateTimeOffset now, CancellationToken cancellationToken);
}