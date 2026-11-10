using LivePoll.Application;
using LivePoll.Infrastructure.Ruvio;

namespace LivePoll.Integration.Voting;

[Collection(VoteFlowGroup.Name)]
public sealed class BusinessMetricsStorageTests(VoteFlowFixture stack)
{
    [Fact(DisplayName = "BusinessMetricsStorageTests.FiveCountersAreReadFromRuvio")]
    public async Task FiveCountersAreReadFromRuvio()
    {
        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        var optionA = poll.Options[0];
        var optionB = poll.Options[1];
        var totalBefore = await stack.Client.GetInt64Async(RuvioKeys.TotalVotes) ?? 0;
        var backlogBefore = await stack.Client.GetInt64Async(RuvioKeys.Backlog) ?? 0;

        await stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", optionA.Id);

        var totalKey = await stack.Client.GetInt64Async(RuvioKeys.TotalVotes);
        var optionKey = await stack.Client.GetInt64Async(RuvioKeys.OptionVotes(poll.Id, optionA.Id));
        var missingOption = await stack.Client.GetInt64Async(RuvioKeys.OptionVotes(poll.Id, optionB.Id));
        var backlogKey = await stack.Client.GetInt64Async(RuvioKeys.Backlog);
        Assert.Equal(totalBefore + 1, totalKey);
        Assert.Equal(1L, optionKey);
        Assert.Null(missingOption);
        Assert.Equal(backlogBefore + 1, backlogKey);

        var entryId = await stack.SingleEntryIdAsync(poll.Id);
        await stack.WaitUntilEntryIsInPreviousSecondAsync(entryId);

        var view = await stack.ReadMetricsAsync(clock, poll);
        var workerVotesPerSecond = await stack.PublishingWorkerVotesPerSecondAsync(poll.Id);
        var endTimes = await new RuvioBusinessMetricsStore(stack.Client).GetActivePollEndTimesAsync(CancellationToken.None);

        Assert.Equal(totalKey, view.TotalVotes);
        Assert.Equal(optionKey, view.VotesPerOption.Single(option => option.OptionId == optionA.Id).Votes);
        Assert.Equal(0L, view.VotesPerOption.Single(option => option.OptionId == optionB.Id).Votes);
        Assert.Equal(backlogKey, view.VotesWaitingForPostgres);
        Assert.Contains(poll.EndsAt, endTimes);
        Assert.Equal(ActivePolls.CountByEndTime(endTimes, clock.UtcNow), view.ActivePolls);
        Assert.Equal(workerVotesPerSecond, view.VotesPerSecond);
        Assert.True(view.VotesPerSecond >= 1);
    }
}
