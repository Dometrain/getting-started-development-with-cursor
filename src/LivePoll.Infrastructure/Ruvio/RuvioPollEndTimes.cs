using System.Globalization;

using LivePoll.Application;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

public sealed class RuvioPollEndTimes(IRuvioClient client) : IPollEndTimeReader, IPollEndTimeWriter
{
    public async Task SaveEndsAtAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
    {
        var millis = endsAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        await client.SetAsync(RuvioKeys.EndsAt(pollId), millis, cancellationToken);
    }

    public async Task<DateTimeOffset?> GetEndsAtAsync(Guid pollId, CancellationToken cancellationToken)
    {
        var stored = await client.GetStringAsync(RuvioKeys.EndsAt(pollId), cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var millis = long.Parse(stored, CultureInfo.InvariantCulture);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis);
    }
}