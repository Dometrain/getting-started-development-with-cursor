using System.Collections.Concurrent;

namespace LivePoll.Application;

public sealed class VoteAcceptanceService
{
    private readonly IPollEndTimeReader endTimes;
    private readonly IOneVoteMarker oneVoteMarker;
    private readonly IVoteQueue voteQueue;
    private readonly IAcceptanceMetrics acceptanceMetrics;
    private readonly IClock clock;
    private readonly ConcurrentDictionary<Guid, Lazy<Task<DateTimeOffset>>> endsAtCache = new();

    public VoteAcceptanceService(
        IPollEndTimeReader endTimes,
        IOneVoteMarker oneVoteMarker,
        IVoteQueue voteQueue,
        IAcceptanceMetrics acceptanceMetrics,
        IClock clock)
    {
        this.endTimes = endTimes;
        this.oneVoteMarker = oneVoteMarker;
        this.voteQueue = voteQueue;
        this.acceptanceMetrics = acceptanceMetrics;
        this.clock = clock;
    }

    public async Task<VoteAcceptanceResult> AcceptAsync(
        Guid pollId,
        string username,
        Guid optionId,
        CancellationToken cancellationToken = default)
    {
        EnsureSignedIn(username);

        var endsAt = await GetEndsAtAsync(pollId, cancellationToken);
        if (clock.UtcNow >= endsAt)
        {
            throw new ApplicationException("This poll is closed. It was open for 10 minutes.");
        }

        if (!await oneVoteMarker.TryAcceptAsync(pollId, username, cancellationToken))
        {
            throw new ApplicationException("This username has already voted on this poll.");
        }

        await voteQueue.EnqueueAsync(pollId, username, optionId, cancellationToken);
        await acceptanceMetrics.IncrementOnAcceptanceAsync(pollId, optionId, cancellationToken);

        return new VoteAcceptanceResult
        {
            PollId = pollId,
            OptionId = optionId,
            Username = username,
            AcceptedForStorage = true,
        };
    }

    private async Task<DateTimeOffset> GetEndsAtAsync(Guid pollId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = endsAtCache.GetOrAdd(
            pollId,
            static (id, service) => new Lazy<Task<DateTimeOffset>>(() => service.ReadEndsAtAsync(id)),
            this);

        try
        {
            return await pending.Value.WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && pending.Value.IsFaulted)
        {
            endsAtCache.TryRemove(pollId, out _);
            throw;
        }
    }

    private async Task<DateTimeOffset> ReadEndsAtAsync(Guid pollId)
    {
        var endsAt = await endTimes.GetEndsAtAsync(pollId, CancellationToken.None);
        if (endsAt is null)
        {
            throw new ApplicationException("Poll was not found.");
        }

        return endsAt.Value;
    }

    private static void EnsureSignedIn(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ApplicationException("A signed-in user is required.");
        }
    }
}