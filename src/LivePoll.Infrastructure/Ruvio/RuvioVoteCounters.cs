using LivePoll.Application;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

public sealed class RuvioVoteCounters(IRuvioClient client) : IAcceptanceMetrics
{
    public async Task IncrementOnAcceptanceAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken)
    {
        await client.IncrementAsync(RuvioKeys.TotalVotes, cancellationToken);
        await client.IncrementAsync(RuvioKeys.OptionVotes(pollId, optionId), cancellationToken);
        await client.IncrementAsync(RuvioKeys.Backlog, cancellationToken);
    }

    public async Task DecrementBacklogOnceAsync(CancellationToken cancellationToken)
    {
        await client.DecrementAsync(RuvioKeys.Backlog, cancellationToken);
    }
}
