using LivePoll.Infrastructure.Ruvio;

namespace LivePoll.Worker.Voting;

public sealed class RuvioBacklogCounter(RuvioVoteCounters counters) : IBacklogCounter
{
    public Task DecrementOnceAsync(CancellationToken cancellationToken) =>
        counters.DecrementBacklogOnceAsync(cancellationToken);
}