using System.Globalization;

using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Worker;

public sealed class RuvioActivePollCleaner(IRuvioClient client) : IActivePollCleaner
{
    public async Task RemoveEndedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var maxScore = now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        await client.RemoveSortedSetRangeByScoreAsync(
            RuvioKeys.ActivePolls,
            "-inf",
            maxScore,
            cancellationToken);
    }
}