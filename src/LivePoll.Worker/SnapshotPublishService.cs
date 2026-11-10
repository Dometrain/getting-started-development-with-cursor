namespace LivePoll.Worker;

public sealed class SnapshotPublishService(SnapshotPublisher publisher) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await publisher.PublishUntilCancelledAsync(stoppingToken);
    }
}