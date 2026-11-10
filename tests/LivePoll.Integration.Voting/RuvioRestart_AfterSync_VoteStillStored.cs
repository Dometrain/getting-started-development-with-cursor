using PollException = LivePoll.Application.ApplicationException;

namespace LivePoll.Integration.Voting;

public sealed class RuvioRestart_AfterSync_VoteStillStored
{
    [Fact(DisplayName = "RuvioRestart_AfterSync_VoteStillStored")]
    public async Task AfterEverysecSync_RestartKeepsDataDirectory_AndStoresOnce()
    {
        await using var stack = new VoteFlowFixture();
        await stack.InitializeAsync();
        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        var optionId = poll.Options[0].Id;
        await stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", optionId);
        Assert.Equal(1, await stack.QueuedForPollAsync(poll.Id));

        await Task.Delay(RuvioSession.EverysecSyncWait);
        await stack.RestartRuvioAsync();

        var environment = await DockerInspect.ReadAsync(
            stack.RuvioContainerId,
            "{{range .Config.Env}}{{println .}}{{end}}");
        Assert.Contains("RUVIO_DURABILITY=everysec", environment, StringComparison.Ordinal);
        Assert.Equal(1L, await stack.OnceExistsAsync(poll.Id, "ada"));
        Assert.Equal(1, await stack.QueuedForPollAsync(poll.Id));

        await stack.StorePendingAsync(clock);

        var stored = Assert.Single(await stack.ReadVotesAsync(poll.Id));
        Assert.Equal("ada", stored.UserId);
        Assert.Equal(optionId, stored.OptionId);

        var repeat = await Assert.ThrowsAsync<PollException>(() =>
            stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", optionId));
        Assert.Equal("This username has already voted on this poll.", repeat.Message);
        Assert.Single(await stack.ReadVotesAsync(poll.Id));
    }
}
