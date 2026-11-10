using System.Diagnostics;

namespace LivePoll.Integration.Voting;

[Collection(VoteFlowGroup.Name)]
public sealed class AcceptedVote_ReachesPostgres_Within5Seconds(VoteFlowFixture stack)
{
    [Fact(DisplayName = "AcceptedVote_ReachesPostgres_Within5Seconds")]
    public async Task StorableVote_IsOnePostgresRow_Within5Seconds()
    {
        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        var optionId = poll.Options[0].Id;
        var elapsed = Stopwatch.StartNew();
        await stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", optionId);
        await stack.StorePendingAsync(clock);
        var stored = await stack.ReadVotesAsync(poll.Id);
        elapsed.Stop();

        var vote = Assert.Single(stored);
        Assert.Equal("ada", vote.UserId);
        Assert.Equal(optionId, vote.OptionId);
        Assert.True(elapsed.Elapsed <= TimeSpan.FromSeconds(5));
    }
}
