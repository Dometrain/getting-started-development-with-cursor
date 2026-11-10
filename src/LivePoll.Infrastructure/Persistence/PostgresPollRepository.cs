using LivePoll.Application;
using LivePoll.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivePoll.Infrastructure.Persistence;

public sealed class PostgresPollRepository(IServiceProvider services) : IPollRepository
{
    public async Task SaveAsync(StoredPoll poll, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
        var aggregate = poll.Aggregate;
        var existing = await db.Polls
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == aggregate.Id, cancellationToken);
        if (existing is null)
        {
            db.Polls.Add(new PollRecord
            {
                Id = aggregate.Id,
                Title = aggregate.Title,
                CreatedAt = poll.CreatedAt,
                EndsAt = poll.EndsAt,
                Options = aggregate.Options
                    .Select(option => new PollOptionRecord
                    {
                        Id = option.Id,
                        PollId = aggregate.Id,
                        Text = option.Text,
                    })
                    .ToList(),
            });
        }
        else
        {
            existing.Title = aggregate.Title;
            existing.EndsAt = poll.EndsAt;
            var incoming = aggregate.Options.Select(option => option.Id).ToHashSet();
            var removed = existing.Options.Where(option => !incoming.Contains(option.Id)).ToList();
            foreach (var option in removed)
            {
                existing.Options.Remove(option);
            }

            foreach (var option in aggregate.Options)
            {
                var row = existing.Options.FirstOrDefault(item => item.Id == option.Id);
                if (row is null)
                {
                    existing.Options.Add(new PollOptionRecord
                    {
                        Id = option.Id,
                        PollId = aggregate.Id,
                        Text = option.Text,
                    });
                }
                else
                {
                    row.Text = option.Text;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredPoll?> GetAsync(Guid pollId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
        var record = await db.Polls
            .AsNoTracking()
            .Include(item => item.Options)
            .Include(item => item.Votes)
            .FirstOrDefaultAsync(item => item.Id == pollId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return new StoredPoll
        {
            Aggregate = Poll.Restore(
                record.Id,
                record.Title,
                record.Options.OrderBy(option => option.Id).Select(option => (option.Id, option.Text)),
                record.Votes.Select(vote => vote.UserId)),
            CreatedAt = record.CreatedAt,
            EndsAt = record.EndsAt,
        };
    }
}