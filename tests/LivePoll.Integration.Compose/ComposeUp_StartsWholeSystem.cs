using System.Net;

namespace LivePoll.Integration.Compose;

[Collection(ComposeGroup.Name)]
public sealed class ComposeUp_StartsWholeSystem(ComposeFixture stack)
{
    [Fact(DisplayName = "ComposeUp_StartsWholeSystem")]
    public async Task CreatePoll_CastVote_AndViewResults()
    {
        var apiKey = await stack.EnvironmentValueAsync("api1", "Jwt__SigningKey");
        Assert.Equal(apiKey, await stack.EnvironmentValueAsync("api2", "Jwt__SigningKey"));
        Assert.Equal(apiKey, await stack.EnvironmentValueAsync("bff", "Jwt__SigningKey"));

        using var client = LivePollClient.ForOrigin(stack.Origin);
        await client.WaitUntilReadyAsync(CancellationToken.None);

        var voter = await client.SignInAsync("ada", CancellationToken.None);
        var poll = await client.CreatePollAsync(voter, CancellationToken.None);
        await client.VoteAsync(voter, poll.Id, poll.FirstOptionId, CancellationToken.None);
        await client.WaitForVotesAsync(poll.Id, poll.FirstOptionId, 1, CancellationToken.None);

        var admin = await client.SignInAsync("ada-admin", CancellationToken.None);
        var simulated = await client.CreatePollAsync(admin, CancellationToken.None);
        using (var forbidden = await client.StartSimulationAsync(voter, simulated, CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using var started = await client.StartSimulationAsync(admin, simulated, CancellationToken.None);
        var startedBody = await started.Content.ReadAsStringAsync();
        Assert.True(started.StatusCode == HttpStatusCode.Created, startedBody);
        Assert.Contains("/admin/simulations/", started.Headers.Location?.ToString(), StringComparison.Ordinal);
        await client.WaitForVotesAsync(simulated.Id, simulated.FirstOptionId, 1, CancellationToken.None);
    }
}