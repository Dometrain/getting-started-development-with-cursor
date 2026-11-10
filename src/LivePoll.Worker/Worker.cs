using LivePoll.Worker.Voting;

namespace LivePoll.Worker;

public sealed class Worker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<VoteStoreProcessor>();
            await processor.ProcessBatchAsync(stoppingToken);
        }
    }
}