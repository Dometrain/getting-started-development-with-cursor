using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LivePoll.Domain;

using Ruvio.Client;

namespace LivePoll.Api.Tests;

[Collection(RuvioApiCollection.Name)]
public sealed class LimitFollowsUserName : IDisposable
{
    public const int PermitCount = 1;
    public const int WindowSeconds = 60;

    private readonly ApiWebApplicationFactory factory;

    public LimitFollowsUserName(RuvioApiFixture ruvio)
    {
        Assert.Equal(1, PermitCount);
        Assert.Equal(60, WindowSeconds);

        factory = new ApiWebApplicationFactory
        {
            OutageMode = RuvioOutageMode.FailClosed,
            RuvioHost = "127.0.0.1",
            RuvioPort = ruvio.Port,
            RuvioPassword = RuvioApiFixture.Password,
            VotePermits = PermitCount,
            VoteWindowSeconds = WindowSeconds,
        };
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task SameClientAddress_UsernameOverFixedWindowGets429_SecondUsernameSucceeds()
    {
        var pollId = Guid.NewGuid();
        factory.EndTimes.SetEndsAt(pollId, factory.Clock.UtcNow.AddSeconds(Poll.OpenDurationSeconds));

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "limit-ada"));
        using var first = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        using var limited = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta is not null || limited.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);

        await using var stream = await limited.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/rate-limited", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(429, document.RootElement.GetProperty("status").GetInt32());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "limit-grace"));
        using var otherUser = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, otherUser.StatusCode);
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
