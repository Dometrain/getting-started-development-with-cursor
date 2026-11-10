using System.Diagnostics;

namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class PublishingWorkerStopStillWithinOneSecond(ResultsFixture stack)
{
    [Fact(DisplayName = "PublishingWorkerStopStillWithinOneSecond")]
    public async Task LeaseHolderStops_NewTotalWithinOneSecond()
    {
        await stack.EnsureReplicasAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var creator = await PublicSite.SignInAsync(client, "worker-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, creator, CancellationToken.None);
        await using var stream = await LiveResultsStream.OpenAsync(client, poll.Id, CancellationToken.None);
        _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        var holder = await stack.LeaseHolderAsync();
        try
        {
            await stack.KillAsync(holder);
            var voter = await PublicSite.SignInAsync(client, "worker-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
            _ = await PublicSite.VoteAsync(client, voter, poll.Id, poll.FirstOptionId, CancellationToken.None);
            var started = Stopwatch.GetTimestamp();
            _ = await stream.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(1), CancellationToken.None);
            var elapsed = Stopwatch.GetElapsedTime(started);
            Assert.True(elapsed <= TimeSpan.FromSeconds(1), $"New total after {holder} stopped took {elapsed}.");
        }
        finally
        {
            await stack.EnsureReplicasAsync();
        }
    }
}