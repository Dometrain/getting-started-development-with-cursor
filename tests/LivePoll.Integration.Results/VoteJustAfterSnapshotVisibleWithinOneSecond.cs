using System.Diagnostics;

namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class VoteJustAfterSnapshotVisibleWithinOneSecond(ResultsFixture stack)
{
    [Fact(DisplayName = "VoteJustAfterSnapshotVisibleWithinOneSecond")]
    public async Task VoteJustAfterASnapshot_IsOnTheStreamWithinOneSecond()
    {
        await stack.EnsureReplicasAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var first = await PublicSite.SignInAsync(client, UserName(), CancellationToken.None);
        var second = await PublicSite.SignInAsync(client, UserName(), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, first, CancellationToken.None);
        await using var stream = await LiveResultsStream.OpenAsync(client, poll.Id, CancellationToken.None);
        _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        _ = await PublicSite.VoteAsync(client, first, poll.Id, poll.FirstOptionId, CancellationToken.None);
        _ = await stream.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(3), CancellationToken.None);

        _ = await PublicSite.VoteAsync(client, second, poll.Id, poll.FirstOptionId, CancellationToken.None);
        var started = Stopwatch.GetTimestamp();
        _ = await stream.ReadUntilVotesAsync(poll.FirstOptionId, 2, TimeSpan.FromSeconds(1), CancellationToken.None);

        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.True(elapsed <= TimeSpan.FromSeconds(1), $"Vote after the snapshot was visible after {elapsed}.");
    }

    private static string UserName() => "after-" + Guid.NewGuid().ToString("N");
}