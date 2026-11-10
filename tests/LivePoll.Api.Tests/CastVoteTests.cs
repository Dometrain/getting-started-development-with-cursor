using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LivePoll.Domain;

namespace LivePoll.Api.Tests;

[Collection(RuvioApiCollection.Name)]
public sealed class CastVoteTests : IDisposable
{
    private readonly RuvioBackedApiFactory factory;

    public CastVoteTests(RuvioApiFixture ruvio)
    {
        factory = new RuvioBackedApiFactory(ruvio);
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task CastVote_ValidToken_Returns202WithUsernameFromToken()
    {
        var pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
        var optionId = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
        SeedOpenPoll(pollId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "ada"));

        using var response = await client.PostAsJsonAsync(
            $"/polls/{pollId}/votes",
            new { optionId });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoteAcceptedBody>();
        Assert.NotNull(body);
        Assert.Equal(pollId, body.PollId);
        Assert.Equal(optionId, body.OptionId);
        Assert.Equal("ada", body.Username);
    }

    [Fact]
    public async Task CastVote_Repeat_Returns409AlreadyVoted()
    {
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        SeedOpenPoll(pollId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "repeat-user"));

        using var first = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        using var second = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        await using var stream = await second.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/already-voted", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(409, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task CastVote_EndedPoll_Returns409PollEnded()
    {
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        factory.EndTimes.SetEndsAt(pollId, factory.Clock.UtcNow.AddMinutes(-1));
        var onceBefore = factory.OneVoteMarker.OnceCalls;
        var xaddBefore = factory.VoteQueue.XaddCalls;

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await SignInAsync(client, "ended-user"));

        using var response = await client.PostAsJsonAsync($"/polls/{pollId}/votes", new { optionId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/poll-ended", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(409, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(onceBefore, factory.OneVoteMarker.OnceCalls);
        Assert.Equal(xaddBefore, factory.VoteQueue.XaddCalls);
    }

    private void SeedOpenPoll(Guid pollId)
    {
        factory.EndTimes.SetEndsAt(
            pollId,
            factory.Clock.UtcNow.AddSeconds(Poll.OpenDurationSeconds));
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

    private sealed record VoteAcceptedBody(Guid PollId, Guid OptionId, string Username);
}
