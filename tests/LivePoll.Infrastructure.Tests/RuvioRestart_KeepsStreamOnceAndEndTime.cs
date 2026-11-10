using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Tests;

public sealed class RuvioRestart_KeepsStreamOnceAndEndTime
{
    [Fact]
    public async Task AfterEverysecSync_KeepsStreamOnceAndEndTime_AndDropsSnapshot()
    {
        await using var session = new RuvioContainerSession();
        await session.StartAsync();

        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var endsAt = new DateTimeOffset(2026, 11, 7, 19, 0, 0, TimeSpan.Zero);
        var marker = new RuvioOneVoteMarker(session.Client);
        var queue = new RuvioVoteQueue(session.Client);
        var endTimes = new RuvioPollEndTimes(session.Client);
        var snapshots = new RuvioLiveSnapshots(session.Client);
        var published = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await session.Client.SubscribeAsync(
            RuvioKeys.ResultsChannel,
            message => published.TrySetResult(message.Payload));
        Assert.True(await marker.TryAcceptAsync(pollId, "ada", CancellationToken.None));
        await queue.EnqueueAsync(pollId, "ada", optionId, CancellationToken.None);
        var entryId = Assert.Single(await session.Client.GetStreamRangeAsync(
            RuvioKeys.VoteStream,
            StreamIds.Min,
            StreamIds.Max,
            false)).Id;
        await endTimes.SaveEndsAtAsync(pollId, endsAt, CancellationToken.None);
        await snapshots.PublishAsync("snapshot-before-restart", CancellationToken.None);
        Assert.Equal("snapshot-before-restart", await published.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        await Task.Delay(RuvioContainerSession.EverysecSyncWait);
        await session.RestartAsync();
        marker = new RuvioOneVoteMarker(session.Client);
        endTimes = new RuvioPollEndTimes(session.Client);

        var entries = await session.Client.GetStreamRangeAsync(
            RuvioKeys.VoteStream,
            StreamIds.Min,
            StreamIds.Max,
            false);
        var entry = Assert.Single(entries);
        Assert.Equal(entryId, entry.Id);
        Assert.Equal(1L, await session.Client.ExistsAsync(RuvioKeys.Once(pollId, "ada")));
        Assert.False(await marker.TryAcceptAsync(pollId, "ada", CancellationToken.None));
        Assert.Equal(endsAt, await endTimes.GetEndsAtAsync(pollId, CancellationToken.None));
        Assert.Equal(-1L, await session.Client.GetTimeToLiveAsync(RuvioKeys.EndsAt(pollId)));

        var replayed = new List<string>();
        await session.Client.SubscribeAsync(
            RuvioKeys.ResultsChannel,
            message => replayed.Add(message.Payload));
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.Empty(replayed);
    }
}
