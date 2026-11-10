namespace LivePoll.Integration.Load;

[Collection(LoadGroup.Name)]
public sealed class WorkerCrash_BeforeAck_VoteStoredOnce(LoadFixture stack)
{
    [Fact(DisplayName = "WorkerCrash_BeforeAck_VoteStoredOnce")]
    public async Task AcceptedVote_WorkerKilledBeforeAck_StoredOnceUnderUsername()
    {
        await stack.EnsureWorkersAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var username = "crash-" + Guid.NewGuid().ToString("N");
        var token = await PublicSite.SignInAsync(client, username, CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);
        await stack.PauseAsync("worker1", "worker2");
        try
        {
            _ = await PublicSite.VoteAsync(client, token, poll.Id, poll.FirstOptionId, CancellationToken.None);
            Assert.Equal(0, await stack.CountVotesAsync(poll.Id, username));

            await stack.KillAsync("worker1");
            await stack.UnpauseAsync("worker2");
            await stack.WaitForVotesAsync(poll.Id, username, 1, TimeSpan.FromSeconds(15));

            Assert.Equal(1, await stack.CountVotesAsync(poll.Id, username));
        }
        finally
        {
            _ = await Docker.ComposeAsync(stack.Root, TimeSpan.FromMinutes(2), "unpause", "worker1", "worker2");
            await stack.StartAsync("worker1", "worker2");
        }
    }
}
