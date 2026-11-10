using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

public sealed class RuvioLiveSnapshots(IRuvioClient client)
{
    public async Task PublishAsync(string payload, CancellationToken cancellationToken)
    {
        await client.PublishAsync(RuvioKeys.ResultsChannel, payload, cancellationToken);
    }
}
