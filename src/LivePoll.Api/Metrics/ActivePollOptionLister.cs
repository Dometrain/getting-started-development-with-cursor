using LivePoll.Application;

namespace LivePoll.Api.Metrics;

public sealed class ActivePollOptionLister(
    IActivePollDirectory directory,
    IPollRepository polls,
    IClock clock)
{
    public async Task<IReadOnlyList<OptionMetricQuery>> ListAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var entries = await directory.ListAsync(cancellationToken);
        var queries = new List<OptionMetricQuery>();
        foreach (var entry in entries)
        {
            if (entry.EndsAt <= now)
            {
                continue;
            }

            var stored = await polls.GetAsync(entry.PollId, cancellationToken);
            if (stored is null)
            {
                continue;
            }

            foreach (var option in stored.Aggregate.Options)
            {
                queries.Add(new OptionMetricQuery(entry.PollId, option.Id, option.Text));
            }
        }

        return queries;
    }
}
