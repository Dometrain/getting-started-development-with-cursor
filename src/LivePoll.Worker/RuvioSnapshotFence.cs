using System.Globalization;

using Ruvio.Client;

namespace LivePoll.Worker;

public sealed class RuvioSnapshotFence(IRuvioClient client) : ISnapshotFence
{
    public async Task<bool> TryAcceptAsync(Guid pollId, long fencingToken, CancellationToken cancellationToken)
    {
        var key = $"results:fence:{pollId:D}";
        var current = await client.GetStringAsync(key, cancellationToken);
        if (current is not null
            && long.TryParse(current, NumberStyles.None, CultureInfo.InvariantCulture, out var newest)
            && fencingToken < newest)
        {
            return false;
        }

        await client.SetAsync(
            key,
            fencingToken.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
        return true;
    }
}