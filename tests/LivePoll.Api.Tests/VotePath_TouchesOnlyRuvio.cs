using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LivePoll.Domain;

namespace LivePoll.Api.Tests;

[Collection(RuvioApiCollection.Name)]
public sealed class VotePath_TouchesOnlyRuvio : IDisposable
{
    private readonly RuvioBackedApiFactory factory;

    public VotePath_TouchesOnlyRuvio(RuvioApiFixture ruvio)
    {
        factory = new RuvioBackedApiFactory(ruvio);
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task AcceptPath_ReadsEndTime_CallsOnceAndXadd_UpdatesCounters_NoPostgres()
    {
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        factory.EndTimes.SetEndsAt(pollId, factory.Clock.UtcNow.AddSeconds(Poll.OpenDurationSeconds));
        var onceBefore = factory.OneVoteMarker.OnceCalls;
        var xaddBefore = factory.VoteQueue.XaddCalls;
        var countersBefore = factory.AcceptanceMetrics.CounterUpdates;
        var endReadsBefore = factory.EndTimes.ReadCount;
        var postgresBefore = factory.PostgresTouches.Touches;

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "path-user"));

        using var response = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(endReadsBefore + 1, factory.EndTimes.ReadCount);
        Assert.Equal(onceBefore + 1, factory.OneVoteMarker.OnceCalls);
        Assert.Equal(xaddBefore + 1, factory.VoteQueue.XaddCalls);
        Assert.Equal(countersBefore + 1, factory.AcceptanceMetrics.CounterUpdates);
        Assert.Equal(postgresBefore, factory.PostgresTouches.Touches);
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
