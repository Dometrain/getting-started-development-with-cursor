using LivePoll.Application;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

public sealed class RuvioActivePolls(IRuvioClient client) : IActivePollWriter
{
    public async Task AddAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
    {
        await client.AddSortedSetAsync(
            RuvioKeys.ActivePolls,
            pollId.ToString("D"),
            endsAt.ToUnixTimeMilliseconds(),
            cancellationToken);
    }
}