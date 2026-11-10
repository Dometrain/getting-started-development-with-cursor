using PollException = LivePoll.Application.ApplicationException;

namespace LivePoll.Integration.Voting;

[Collection(VoteFlowGroup.Name)]
public sealed class OneVotePerPoll_DuplicateAndSameMoment(VoteFlowFixture stack)
{
    [Fact(DisplayName = "OneVotePerPoll_DuplicateAndSameMoment")]
    public async Task SameUsernameTwice_IncludingOverlap_StoresOneRow()
    {
        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        var optionId = poll.Options[0].Id;
        var first = stack.Acceptance(clock);
        var second = stack.Acceptance(clock, await stack.OpenClientAsync());

        var results = await Task.WhenAll(
            AcceptanceAttempts.RecordAsync(first.AcceptAsync(poll.Id, "ada", optionId)),
            AcceptanceAttempts.RecordAsync(second.AcceptAsync(poll.Id, "ada", optionId)));

        Assert.Equal(1, results.Count(result => result.Accepted));
        Assert.Contains(results, result => result.Error?.Message == "This username has already voted on this poll.");

        var repeat = await Assert.ThrowsAsync<PollException>(() => first.AcceptAsync(poll.Id, "ada", optionId));
        Assert.Equal("This username has already voted on this poll.", repeat.Message);
        Assert.Equal(1, await stack.QueuedForPollAsync(poll.Id));
        Assert.Equal(1L, await stack.OnceExistsAsync(poll.Id, "ada"));

        await stack.StorePendingAsync(clock);

        var stored = Assert.Single(await stack.ReadVotesAsync(poll.Id));
        Assert.Equal("ada", stored.UserId);
        Assert.Equal(optionId, stored.OptionId);
    }
}
