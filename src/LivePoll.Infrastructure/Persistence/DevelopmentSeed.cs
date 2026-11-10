using LivePoll.Application;

using Microsoft.EntityFrameworkCore;

namespace LivePoll.Infrastructure.Persistence;

public static class DevelopmentSeed
{
    public static async Task ApplyAsync(
        LivePollDbContext db,
        IPollEndTimeWriter endTimes,
        IActivePollWriter activePolls,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(endTimes);
        ArgumentNullException.ThrowIfNull(activePolls);
        if (await db.Polls.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var endsAt = now.AddSeconds(600);
        var polls = new List<PollRecord>
        {
            CreatePoll(
                "Favorite lunch spot?",
                ["Cafe Nora", "Harbor Deli", "Spice Route", "Green Bowl"],
                now,
                endsAt),
            CreatePoll(
                "Team outing activity?",
                ["Bowling", "Museum", "Escape room"],
                now,
                endsAt),
            CreatePoll(
                "Next sprint focus?",
                ["Reliability", "New features", "Docs", "Tooling"],
                now,
                endsAt),
        };

        db.Polls.AddRange(polls);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var poll in polls)
        {
            await endTimes.SaveEndsAtAsync(poll.Id, poll.EndsAt, cancellationToken);
            await activePolls.AddAsync(poll.Id, poll.EndsAt, cancellationToken);
        }
    }

    private static PollRecord CreatePoll(
        string title,
        IReadOnlyList<string> optionTexts,
        DateTimeOffset createdAt,
        DateTimeOffset endsAt)
    {
        var pollId = Guid.NewGuid();
        return new PollRecord
        {
            Id = pollId,
            Title = title,
            CreatedAt = createdAt,
            EndsAt = endsAt,
            Options = optionTexts
                .Select(text => new PollOptionRecord
                {
                    Id = Guid.NewGuid(),
                    PollId = pollId,
                    Text = text,
                })
                .ToList(),
        };
    }
}