namespace LivePoll.Integration.Load;

[Collection(LoadGroup.Name)]
public sealed class OtherReplicaCompletesTheVote(LoadFixture stack)
{
    [Fact(DisplayName = "OtherReplicaCompletesTheVote")]
    public async Task OneApiAccepts_OtherWorkerStores_OtherApiAcceptsSameToken()
    {
        await stack.EnsureWorkersAsync();
        await stack.KillAsync("worker2");
        using var client = PublicSite.CreateClient(stack.Origin);
        try
        {
            var username = "replica-" + Guid.NewGuid().ToString("N");
            var token = await PublicSite.SignInAsync(client, username, CancellationToken.None);
            var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);
            var accepted = await PublicSite.VoteAsync(client, token, poll.Id, poll.FirstOptionId, CancellationToken.None);

            Assert.True(accepted.Destination is "api1" or "api2", accepted.Destination ?? "");
            await stack.WaitForVotesAsync(poll.Id, username, 1, TimeSpan.FromSeconds(15));
            Assert.Equal(1, await stack.CountVotesAsync(poll.Id, username));
            Assert.True(await stack.IsRunningAsync("worker1"));
            Assert.False(await stack.IsRunningAsync("worker2"));

            var later = await PublicSite.OtherApiAsync(client, token, accepted.Destination!, CancellationToken.None);
            Assert.NotEqual(accepted.Destination, later);
        }
        finally
        {
            await stack.StartAsync("worker2");
        }
    }
}
