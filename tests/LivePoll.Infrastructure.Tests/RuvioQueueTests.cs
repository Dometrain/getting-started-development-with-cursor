using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Tests;

[Collection(RuvioContainerGroup.Name)]
public sealed class RuvioQueueTests(RuvioFixture fixture)
{
    [Fact]
    public async Task Acceptance_IncrementsTotalOptionAndBacklog()
    {
        await fixture.ResetAsync();
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var counters = new RuvioVoteCounters(fixture.Client);

        await counters.IncrementOnAcceptanceAsync(pollId, optionId, CancellationToken.None);

        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.TotalVotes));
        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.OptionVotes(pollId, optionId)));
        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.Backlog));
    }

    [Fact]
    public async Task StorePath_DecrementsBacklogOnce()
    {
        await fixture.ResetAsync();
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var counters = new RuvioVoteCounters(fixture.Client);
        await counters.IncrementOnAcceptanceAsync(pollId, optionId, CancellationToken.None);

        await counters.DecrementBacklogOnceAsync(CancellationToken.None);

        Assert.Equal<long?>(0, await fixture.Client.GetInt64Async(RuvioKeys.Backlog));
        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.TotalVotes));
        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.OptionVotes(pollId, optionId)));
    }

    [Fact]
    public async Task Acceptance_IsVisibleToTheGroupWithoutDelay()
    {
        await fixture.ResetAsync();
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var queue = new RuvioVoteQueue(fixture.Client);
        await queue.EnqueueAsync(pollId, "ada", optionId, CancellationToken.None);
        await fixture.Client.CreateStreamGroupAsync(
            RuvioKeys.VoteStream,
            RuvioKeys.ConsumerGroup,
            "0");

        var read = await fixture.Client.GetStreamGroupUpdatesAsync(
            RuvioKeys.ConsumerGroup,
            "worker-1",
            RuvioKeys.VoteStream,
            StreamIds.NewMessages);

        var entry = Assert.Single(Assert.Single(read).Entries);
        Assert.Equal(pollId.ToString("D"), Field(entry, RuvioKeys.PollIdField));
        Assert.Equal("ada", Field(entry, RuvioKeys.UsernameField));
        Assert.Equal(optionId.ToString("D"), Field(entry, RuvioKeys.OptionIdField));

        var source = File.ReadAllText(Path.Combine(RepoRoot.Find(), "src", "LivePoll.Infrastructure", "Ruvio", "RuvioVoteQueue.cs"));
        Assert.Contains("AddStreamEntryAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddStreamEntryDelayedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DELAY", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MAXLEN", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backlog_StaysTheCounterWhenPendingAndLengthChange()
    {
        await fixture.ResetAsync();
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var counters = new RuvioVoteCounters(fixture.Client);
        var queue = new RuvioVoteQueue(fixture.Client);
        await counters.IncrementOnAcceptanceAsync(pollId, optionId, CancellationToken.None);
        await queue.EnqueueAsync(pollId, "ada", optionId, CancellationToken.None);
        await fixture.Client.CreateStreamGroupAsync(
            RuvioKeys.VoteStream,
            RuvioKeys.ConsumerGroup,
            "0");
        var read = await fixture.Client.GetStreamGroupUpdatesAsync(
            RuvioKeys.ConsumerGroup,
            "worker-1",
            RuvioKeys.VoteStream,
            StreamIds.NewMessages);
        var entryId = Assert.Single(Assert.Single(read).Entries).Id;

        Assert.Equal(1L, await fixture.Client.GetStreamLengthAsync(RuvioKeys.VoteStream));
        Assert.Equal(1L, PendingCount(await fixture.Client.GetStreamPendingAsync(RuvioKeys.VoteStream, RuvioKeys.ConsumerGroup)));
        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.Backlog));

        await fixture.Client.AcknowledgeStreamAsync(RuvioKeys.VoteStream, RuvioKeys.ConsumerGroup, [entryId]);

        Assert.Equal(1L, await fixture.Client.GetStreamLengthAsync(RuvioKeys.VoteStream));
        Assert.Equal(0L, PendingCount(await fixture.Client.GetStreamPendingAsync(RuvioKeys.VoteStream, RuvioKeys.ConsumerGroup)));
        Assert.Equal<long?>(1, await fixture.Client.GetInt64Async(RuvioKeys.Backlog));
    }

    [Fact]
    public async Task EndTime_HasNoExpiry()
    {
        await fixture.ResetAsync();
        var pollId = Guid.NewGuid();
        var endsAt = new DateTimeOffset(2026, 11, 7, 18, 10, 0, TimeSpan.Zero);
        var endTimes = new RuvioPollEndTimes(fixture.Client);

        await endTimes.SaveEndsAtAsync(pollId, endsAt, CancellationToken.None);

        Assert.Equal(endsAt, await endTimes.GetEndsAtAsync(pollId, CancellationToken.None));
        Assert.Equal(-1L, await fixture.Client.GetTimeToLiveAsync(RuvioKeys.EndsAt(pollId)));
        Assert.Equal(-1L, await fixture.Client.GetTimeToLiveMillisecondsAsync(RuvioKeys.EndsAt(pollId)));
        Assert.Null(await endTimes.GetEndsAtAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ActivePolls_AreASortedSetScoredByEndTime()
    {
        await fixture.ResetAsync();
        var soonerPoll = Guid.NewGuid();
        var laterPoll = Guid.NewGuid();
        var sooner = new DateTimeOffset(2026, 11, 7, 18, 2, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 11, 7, 18, 10, 0, TimeSpan.Zero);
        var activePolls = new RuvioActivePolls(fixture.Client);
        var store = new RuvioBusinessMetricsStore(fixture.Client);

        await activePolls.AddAsync(laterPoll, later, CancellationToken.None);
        await activePolls.AddAsync(soonerPoll, sooner, CancellationToken.None);

        Assert.Equal<double?>(
            sooner.ToUnixTimeMilliseconds(),
            await fixture.Client.GetSortedSetScoreAsync(RuvioKeys.ActivePolls, soonerPoll.ToString("D")));
        Assert.Equal<double?>(
            later.ToUnixTimeMilliseconds(),
            await fixture.Client.GetSortedSetScoreAsync(RuvioKeys.ActivePolls, laterPoll.ToString("D")));
        Assert.Equal([sooner, later], await store.GetActivePollEndTimesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task VoteStream_EntryIdsUseRuvioServerTime()
    {
        await fixture.ResetAsync();
        var queue = new RuvioVoteQueue(fixture.Client);
        await queue.EnqueueAsync(Guid.NewGuid(), "ada", Guid.NewGuid(), CancellationToken.None);
        var store = new RuvioBusinessMetricsStore(fixture.Client);

        var (entryIds, serverUnixMilliseconds) = await store.GetVoteStreamEntryIdsAsync(CancellationToken.None);

        var entryId = Assert.Single(entryIds);
        Assert.Contains('-', entryId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.InRange(serverUnixMilliseconds, now - 60_000, now + 60_000);
    }

    private static string Field(StreamEntry entry, string name) =>
        entry.Fields.Single(pair => pair.Key == name).Value;

    private static long PendingCount(RespValue summary)
    {
        var items = summary.Items ?? throw new InvalidOperationException("XPENDING summary was empty.");
        return items[0].Integer;
    }

}
