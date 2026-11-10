using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LivePoll.Domain;

namespace LivePoll.Api.Tests;

[Collection(RuvioApiCollection.Name)]
public sealed class EndTimeCachedAfterFirstRead : IDisposable
{
    private readonly RuvioBackedApiFactory factory;

    public EndTimeCachedAfterFirstRead(RuvioApiFixture ruvio)
    {
        factory = new RuvioBackedApiFactory(ruvio);
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task SecondVoteForSamePoll_DoesNotReReadEndTimeKey()
    {
        var pollId = Guid.NewGuid();
        factory.EndTimes.SetEndsAt(pollId, factory.Clock.UtcNow.AddSeconds(Poll.OpenDurationSeconds));

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "cache-ada"));
        using var first = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var readsAfterFirst = factory.EndTimes.ReadCount;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "cache-grace"));
        using var second = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        Assert.Equal(readsAfterFirst, factory.EndTimes.ReadCount);
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
