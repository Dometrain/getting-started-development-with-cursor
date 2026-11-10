using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Api.Metrics;

public sealed class RuvioActivePollDirectory(IRuvioClient client) : IActivePollDirectory
{
    public async Task<IReadOnlyList<ActivePollEntry>> ListAsync(CancellationToken cancellationToken)
    {
        var entries = await client.GetSortedSetRangeByRankWithScoresAsync(
            RuvioKeys.ActivePolls,
            0,
            -1,
            false,
            cancellationToken);
        var polls = new List<ActivePollEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (!Guid.TryParse(entry.Member, out var pollId))
            {
                continue;
            }

            polls.Add(new ActivePollEntry(
                pollId,
                DateTimeOffset.FromUnixTimeMilliseconds((long)entry.Score)));
        }

        return polls;
    }
}
