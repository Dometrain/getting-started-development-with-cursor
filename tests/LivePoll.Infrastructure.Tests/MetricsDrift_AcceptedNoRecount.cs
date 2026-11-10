using LivePoll.Infrastructure.Ruvio;

namespace LivePoll.Infrastructure.Tests;

[Collection(RuvioContainerGroup.Name)]
public sealed class MetricsDrift_AcceptedNoRecount(RuvioFixture fixture)
{
    [Fact]
    public async Task AfterLossWindow_DoesNotRebuildCounters()
    {
        await fixture.ResetAsync();
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var counters = new RuvioVoteCounters(fixture.Client);
        var queue = new RuvioVoteQueue(fixture.Client);
        await counters.IncrementOnAcceptanceAsync(pollId, optionId, CancellationToken.None);
        await queue.EnqueueAsync(pollId, "ada", optionId, CancellationToken.None);
        await fixture.Client.DeleteAsync(
            [RuvioKeys.TotalVotes, RuvioKeys.OptionVotes(pollId, optionId), RuvioKeys.Backlog]);

        var store = new RuvioBusinessMetricsStore(fixture.Client);
        Assert.Null(await store.GetTotalVotesAsync(CancellationToken.None));
        Assert.Null(await store.GetOptionVotesAsync(pollId, optionId, CancellationToken.None));
        Assert.Null(await store.GetAcceptedNotStoredBacklogAsync(CancellationToken.None));
        var (entryIds, _) = await store.GetVoteStreamEntryIdsAsync(CancellationToken.None);

        Assert.NotEmpty(entryIds);
        Assert.Null(await store.GetTotalVotesAsync(CancellationToken.None));
        Assert.Null(await store.GetOptionVotesAsync(pollId, optionId, CancellationToken.None));
        Assert.Null(await store.GetAcceptedNotStoredBacklogAsync(CancellationToken.None));
        Assert.Null(await fixture.Client.GetInt64Async(RuvioKeys.TotalVotes));

        var source = File.ReadAllText(Path.Combine(
            RepoRoot.Find(),
            "src",
            "LivePoll.Infrastructure",
            "Ruvio",
            "RuvioBusinessMetricsStore.cs"));
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Postgres", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Recount", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Rebuild", source, StringComparison.Ordinal);
    }

}
