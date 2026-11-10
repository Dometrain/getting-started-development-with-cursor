using PollException = LivePoll.Application.ApplicationException;

namespace LivePoll.Integration.Voting;

[Collection(VoteFlowGroup.Name)]
public sealed class EndedPoll_VoteRejected(VoteFlowFixture stack)
{
    [Fact(DisplayName = "EndedPoll_VoteRejected")]
    public async Task VoteAfter10Minutes_IsNeitherQueuedNorStored()
    {
        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        Assert.Equal(TimeSpan.FromMinutes(10), poll.EndsAt - poll.CreatedAt);
        clock.UtcNow = poll.EndsAt;

        var error = await Assert.ThrowsAsync<PollException>(() =>
            stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", poll.Options[0].Id));

        Assert.Equal("This poll is closed. It was open for 10 minutes.", error.Message);
        Assert.Equal(0L, await stack.OnceExistsAsync(poll.Id, "ada"));
        Assert.Equal(0, await stack.QueuedForPollAsync(poll.Id));
        Assert.Empty(await stack.ReadVotesAsync(poll.Id));
    }
}
