using LivePoll.Application;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

public sealed class RuvioOneVoteMarker(IRuvioClient client) : IOneVoteMarker
{
    public async Task<bool> TryAcceptAsync(Guid pollId, string username, CancellationToken cancellationToken)
    {
        var created = await client.OnceAsync(RuvioKeys.Once(pollId, username), RuvioKeys.OneVoteTtlMilliseconds, cancellationToken);
        return created == 1;
    }
}
