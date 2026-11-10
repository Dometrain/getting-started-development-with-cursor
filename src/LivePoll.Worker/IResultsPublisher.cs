namespace LivePoll.Worker;

public interface IResultsPublisher
{
    Task PublishAsync(string payload, CancellationToken cancellationToken);
}