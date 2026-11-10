using LivePoll.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivePoll.Worker;

public sealed class PostgresPollOptionSource(IServiceScopeFactory scopes) : IPollOptionSource
{
    public async Task<IReadOnlyList<PollOptionLabel>> ListAsync(Guid pollId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
        var rows = await db.Options
            .AsNoTracking()
            .Where(option => option.PollId == pollId)
            .OrderBy(option => option.Id)
            .Select(option => new { option.Id, option.Text })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new PollOptionLabel(row.Id, row.Text)).ToArray();
    }
}