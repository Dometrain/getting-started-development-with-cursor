using System.Diagnostics;

namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class AcceptedVoteVisibleWithinOneSecond(ResultsFixture stack)
{
    [Fact(DisplayName = "AcceptedVoteVisibleWithinOneSecond")]
    public async Task OpenStream_OneAcceptedVote_VisibleWithinOneSecond()
    {
        await stack.EnsureReplicasAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var token = await PublicSite.SignInAsync(client, UserName(), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);
        await using var stream = await LiveResultsStream.OpenAsync(client, poll.Id, CancellationToken.None);
        _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        _ = await PublicSite.VoteAsync(client, token, poll.Id, poll.FirstOptionId, CancellationToken.None);
        var started = Stopwatch.GetTimestamp();
        _ = await stream.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(1), CancellationToken.None);

        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.True(elapsed <= TimeSpan.FromSeconds(1), $"Vote was visible after {elapsed}.");
    }

    private static string UserName() => "vote-" + Guid.NewGuid().ToString("N");
}