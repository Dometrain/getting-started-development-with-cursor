using System.Diagnostics;

namespace LivePoll.Integration.Load;

[Collection(LoadGroup.Name)]
public sealed class SimulatorLoadTests(LoadFixture stack)
{
    [Fact(DisplayName = "SimulatorLoadTests.OneThousandConcurrentVotersOnOneMachine")]
    public async Task OneThousandConcurrentVotersOnOneMachine()
    {
        await stack.EnsureWorkersAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var admin = await PublicSite.SignInAsync(client, "ada-admin", CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, admin, CancellationToken.None);
        var started = Stopwatch.GetTimestamp();

        _ = await PublicSite.StartSimulationAsync(
            client,
            admin,
            poll.Id,
            poll.FirstOptionId,
            poll.SecondOptionId,
            voterCount: 1000,
            durationSeconds: 120,
            seed: 1000,
            CancellationToken.None);

        await stack.WaitForVotesAsync(poll.Id, null, 1000, TimeSpan.FromSeconds(60));
        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.Equal(1000, await stack.CountVotesAsync(poll.Id, null));
        Assert.True(elapsed < TimeSpan.FromSeconds(60), $"1000 voters took {elapsed}.");
    }
}
