using LivePoll.Application;
using LivePoll.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LivePoll.Worker.Voting;

public sealed class PostgresVoteStore(LivePollDbContext db, IClock clock) : IVoteStore
{
    public async Task<bool> TryStoreOnceAsync(
        Guid pollId,
        string username,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        db.Votes.Add(new VoteRecord
        {
            Id = Guid.NewGuid(),
            PollId = pollId,
            OptionId = optionId,
            UserId = username,
            CastAt = clock.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            db.ChangeTracker.Clear();
            throw new VoteOptionMissingException();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        IsSqlState(exception, PostgresErrorCodes.UniqueViolation);

    private static bool IsForeignKeyViolation(DbUpdateException exception) =>
        IsSqlState(exception, PostgresErrorCodes.ForeignKeyViolation);

    private static bool IsSqlState(DbUpdateException exception, string sqlState) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == sqlState;
}