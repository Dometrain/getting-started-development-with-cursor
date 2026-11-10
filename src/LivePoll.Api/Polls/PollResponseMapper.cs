using LivePoll.Application;
using LivePoll.Domain;

namespace LivePoll.Api.Polls;

public static class PollResponseMapper
{
    public static PollResponse FromView(PollView view) =>
        new(
            view.Id,
            view.Title,
            view.Options.Select(option => new PollOptionResponse(option.Id, option.Text)).ToList(),
            view.DurationSeconds,
            view.CreatedAt,
            view.EndsAt,
            view.Ended);

    public static PollResponse FromStored(StoredPoll stored, IClock clock)
    {
        var now = clock.UtcNow;
        return new PollResponse(
            stored.Aggregate.Id,
            stored.Aggregate.Title,
            stored.Aggregate.Options
                .Select(option => new PollOptionResponse(option.Id, option.Text))
                .ToList(),
            Poll.OpenDurationSeconds,
            stored.CreatedAt,
            stored.EndsAt,
            now >= stored.EndsAt);
    }
}