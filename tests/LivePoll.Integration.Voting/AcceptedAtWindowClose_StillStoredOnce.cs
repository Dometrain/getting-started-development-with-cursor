namespace LivePoll.Integration.Voting;

[Collection(VoteFlowGroup.Name)]
public sealed class AcceptedAtWindowClose_StillStoredOnce(VoteFlowFixture stack)
{
    [Fact]
    public async Task VoteAcceptedAsWindowCloses_IsStoredOnce()
    {
        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        var optionId = poll.Options[0].Id;
        clock.UtcNow = poll.EndsAt.AddTicks(-1);

        var accepted = await stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", optionId);
        Assert.True(accepted.AcceptedForStorage);
        clock.UtcNow = poll.EndsAt.AddMinutes(1);

        await stack.StorePendingAsync(clock);
        await stack.StorePendingAsync(clock);

        var stored = Assert.Single(await stack.ReadVotesAsync(poll.Id));
        Assert.Equal("ada", stored.UserId);
        Assert.Equal(optionId, stored.OptionId);
        Assert.Equal(1L, await stack.OnceExistsAsync(poll.Id, "ada"));
    }
}
