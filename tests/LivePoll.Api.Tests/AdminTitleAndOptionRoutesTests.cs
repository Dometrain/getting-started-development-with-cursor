using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Api.Tests;

public sealed class AdminTitleAndOptionRoutesTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly string[] twoOptions = ["Cafeteria", "Food truck"];

    private readonly ApiWebApplicationFactory factory;

    public AdminTitleAndOptionRoutesTests(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpdateTitle_WithoutAdminRole_Returns403ProblemJson()
    {
        using var client = factory.CreateClient();
        var voterToken = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", voterToken);

        using var created = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });
        var poll = await created.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(poll);

        using var response = await client.PatchAsJsonAsync(
            $"/polls/{poll.Id}",
            new { title = "Dinner spot" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/forbidden", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task AddOption_WithoutAdminRole_Returns403ProblemJson()
    {
        using var client = factory.CreateClient();
        var voterToken = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", voterToken);

        using var created = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });
        var poll = await created.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(poll);

        using var response = await client.PostAsJsonAsync(
            $"/polls/{poll.Id}/options",
            new { text = "Salad bar" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/forbidden", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task UpdateTitle_AfterVote_Returns409PollHasVotes()
    {
        using var client = factory.CreateClient();
        var adminToken = await SignInAsync(client, ApiWebApplicationFactory.AdminUser);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var created = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });
        var poll = await created.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(poll);

        await CastVoteAsync(poll.Id);

        using var response = await client.PatchAsJsonAsync(
            $"/polls/{poll.Id}",
            new { title = "Dinner spot" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/poll-has-votes", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(409, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AddOption_AfterVote_Returns409PollHasVotes()
    {
        using var client = factory.CreateClient();
        var adminToken = await SignInAsync(client, ApiWebApplicationFactory.AdminUser);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var created = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });
        var poll = await created.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(poll);

        await CastVoteAsync(poll.Id);

        using var response = await client.PostAsJsonAsync(
            $"/polls/{poll.Id}/options",
            new { text = "Salad bar" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/poll-has-votes", document.RootElement.GetProperty("type").GetString());
    }

    private async Task CastVoteAsync(Guid pollId)
    {
        var stored = await factory.Polls.GetAsync(pollId, CancellationToken.None);
        Assert.NotNull(stored);
        stored.Aggregate.CastVote("alice", stored.Aggregate.Options[0].Id);
        await factory.Polls.SaveAsync(stored, CancellationToken.None);
    }

    private static async Task<string> SignInAsync(HttpClient client, string username)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);
        return body.Token;
    }

    private sealed record DevTokenBody(string Token, string Username);

    private sealed record PollOptionBody(Guid Id, string Text);

    private sealed record PollBody(
        Guid Id,
        string Title,
        IReadOnlyList<PollOptionBody> Options,
        int DurationSeconds,
        DateTimeOffset CreatedAt,
        DateTimeOffset EndsAt,
        bool Ended);
}