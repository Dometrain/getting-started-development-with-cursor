using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LivePoll.Domain;

using Ruvio.Client;

namespace LivePoll.Api.Tests;

public sealed class VoteLimiter_FailClosed_Returns503
{
    [Fact]
    public async Task WhenRuvioUnavailable_Returns503_AndDoesNotAcceptVote()
    {
        await using var ruvio = new RuvioTestContainer();
        await ruvio.StartAsync();

        using var factory = new ApiWebApplicationFactory
        {
            OutageMode = RuvioOutageMode.FailClosed,
            RuvioHost = "127.0.0.1",
            RuvioPort = ruvio.Port,
            RuvioPassword = RuvioTestContainer.Password,
            VotePermits = 100,
            VoteWindowSeconds = 60,
        };

        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        factory.EndTimes.SetEndsAt(pollId, factory.Clock.UtcNow.AddSeconds(Poll.OpenDurationSeconds));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "fail-closed"));

        using var warm = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId });
        Assert.Equal(HttpStatusCode.Accepted, warm.StatusCode);

        var onceAfterWarm = factory.OneVoteMarker.OnceCalls;
        var xaddAfterWarm = factory.VoteQueue.XaddCalls;

        await ruvio.DisposeAsync();

        var otherPoll = Guid.NewGuid();
        factory.EndTimes.SetEndsAt(otherPoll, factory.Clock.UtcNow.AddSeconds(Poll.OpenDurationSeconds));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "fail-closed-2"));

        using var response = await client.PostAsJsonAsync(
            $"/polls/{otherPoll}/votes",
            new { optionId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(onceAfterWarm, factory.OneVoteMarker.OnceCalls);
        Assert.Equal(xaddAfterWarm, factory.VoteQueue.XaddCalls);
    }

    private static async Task<string> SignInAsync(HttpClient client, string username)
    {
        using var response = await client.PostAsJsonAsync("/auth/dev-token", new { username });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);
        return body.Token;
    }

    private sealed record DevTokenBody(string Token, string Username);
}
