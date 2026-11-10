namespace LivePoll.Integration.Load;

[Collection(LoadGroup.Name)]
public sealed class SimulatorHttpTests(LoadFixture stack)
{
    [Fact(DisplayName = "SimulatorHttpTests.VotesOnlyThroughNginxPublicApi")]
    public async Task VotesOnlyThroughNginxPublicApi()
    {
        await stack.EnsureWorkersAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var admin = await PublicSite.SignInAsync(client, "ada-admin", CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, admin, CancellationToken.None);
        const int seed = 7;

        _ = await PublicSite.StartSimulationAsync(
            client,
            admin,
            poll.Id,
            poll.FirstOptionId,
            poll.SecondOptionId,
            voterCount: 2,
            durationSeconds: 30,
            seed,
            CancellationToken.None);

        await stack.WaitForVotesAsync(poll.Id, $"sim-{seed}-0", 1, TimeSpan.FromSeconds(20));
        await stack.WaitForVotesAsync(poll.Id, $"sim-{seed}-1", 1, TimeSpan.FromSeconds(20));
        Assert.Equal("http://nginx/", await stack.EnvironmentValueAsync("simulator", "PublicApi__BaseAddress"));
        Assert.False(await stack.CanOpenTcpAsync("simulator", "ruvio", 6379));
        Assert.False(await stack.CanOpenTcpAsync("simulator", "postgres", 5432));
    }
}
