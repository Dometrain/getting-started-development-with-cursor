using LivePoll.Infrastructure.Ruvio;

namespace LivePoll.Worker;

public sealed class RuvioResultsPublisher(RuvioLiveSnapshots snapshots) : IResultsPublisher
{
    public Task PublishAsync(string payload, CancellationToken cancellationToken) =>
        snapshots.PublishAsync(payload, cancellationToken);
}