using LivePoll.Application;
using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Worker;

public sealed class RuvioLiveTotals(IRuvioClient client, IBusinessMetricsStore metrics, IPollOptionSource options)
    : ILiveTotals
{
    public async Task<IReadOnlyList<LivePollPicture>> ReadAsync(CancellationToken cancellationToken)
    {
        var members = await client.GetSortedSetRangeByRankWithScoresAsync(
            RuvioKeys.ActivePolls,
            0,
            -1,
            false,
            cancellationToken);
        var (entryIds, serverUnixMilliseconds) = await metrics.GetVoteStreamEntryIdsAsync(cancellationToken);
        var pictures = new List<LivePollPicture>(members.Count);
        foreach (var member in members)
        {
            if (!Guid.TryParse(member.Member, out var pollId))
            {
                continue;
            }

            var totals = await ReadOptionsAsync(pollId, cancellationToken);
            if (totals.Count < 2)
            {
                continue;
            }

            pictures.Add(new LivePollPicture
            {
                PollId = pollId,
                EndsAt = DateTimeOffset.FromUnixTimeMilliseconds((long)member.Score),
                Options = totals,
                StreamEntryIds = entryIds,
                RuvioServerUnixMilliseconds = serverUnixMilliseconds,
            });
        }

        return pictures;
    }

    private async Task<IReadOnlyList<LiveOptionTotal>> ReadOptionsAsync(
        Guid pollId,
        CancellationToken cancellationToken)
    {
        var labels = await options.ListAsync(pollId, cancellationToken);
        var totals = new List<LiveOptionTotal>(labels.Count);
        foreach (var label in labels)
        {
            var votes = await metrics.GetOptionVotesAsync(pollId, label.OptionId, cancellationToken) ?? 0;
            totals.Add(new LiveOptionTotal(label.OptionId, label.Text, votes));
        }

        return totals;
    }
}