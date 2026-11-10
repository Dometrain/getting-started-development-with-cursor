using LivePoll.Worker.Voting;

namespace LivePoll.Worker.Tests;

internal sealed class FakeVoteDeliverySource : IVoteDeliverySource
{
    public FakeVoteDeliverySource(IReadOnlyList<VoteDelivery> initial)
    {
        Queued = [.. initial];
    }

    public List<VoteDelivery> Queued { get; set; }

    public List<string> Acknowledged { get; } = [];

    public List<VoteDelivery> KeptAside { get; } = [];

    public bool AckedAfterStore { get; private set; }

    public FakeVoteStore? Store { get; set; }

    public Task<IReadOnlyList<VoteDelivery>> ReadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<VoteDelivery> batch = Queued.ToArray();
        Queued = [];
        return Task.FromResult(batch);
    }

    public Task AcknowledgeAsync(string entryId, CancellationToken cancellationToken)
    {
        if (Store is not null)
        {
            AckedAfterStore = Store.CompletedStores > 0 && !Acknowledged.Contains(entryId);
        }

        Acknowledged.Add(entryId);
        return Task.CompletedTask;
    }

    public Task KeepAsideAsync(VoteDelivery delivery, CancellationToken cancellationToken)
    {
        KeptAside.Add(delivery);
        return Task.CompletedTask;
    }
}

internal sealed class FakeVoteStore : IVoteStore
{
    private readonly HashSet<string> rows = new(StringComparer.Ordinal);

    public bool FailNextStore { get; set; }

    public bool MissingOption { get; set; }

    public int TryStoreCallCount { get; private set; }

    public int CompletedStores { get; private set; }

    public List<(string Username, Guid OptionId)> StoredVotes { get; } = [];

    public List<string> StoredEntryKeys => rows.ToList();

    public Task<bool> TryStoreOnceAsync(
        Guid pollId,
        string username,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        TryStoreCallCount++;
        if (FailNextStore)
        {
            FailNextStore = false;
            throw new InvalidOperationException("store failed");
        }

        if (MissingOption)
        {
            throw new VoteOptionMissingException();
        }

        var key = $"{pollId:D}:{username}";
        if (!rows.Add(key))
        {
            CompletedStores++;
            return Task.FromResult(false);
        }

        StoredVotes.Add((username, optionId));
        CompletedStores++;
        return Task.FromResult(true);
    }
}

internal sealed class FakeBacklogCounter : IBacklogCounter
{
    public int DecrementCount { get; private set; }

    public Task DecrementOnceAsync(CancellationToken cancellationToken)
    {
        DecrementCount++;
        return Task.CompletedTask;
    }
}

internal static class RepoRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "plan", "tasks.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}