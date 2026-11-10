using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Api.Tests;

public sealed class CreateAndGetPollTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly string[] twoOptions = ["Cafeteria", "Food truck"];

    private readonly ApiWebApplicationFactory factory;

    public CreateAndGetPollTests(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CreatePoll_ValidRequest_Returns201WithDurationAndLocation()
    {
        using var client = factory.CreateClient();
        var token = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var poll = await response.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(poll);
        Assert.Equal(600, poll.DurationSeconds);
        Assert.Equal("Lunch spot", poll.Title);
        Assert.Equal(2, poll.Options.Count);
        Assert.False(poll.Ended);
        Assert.Equal($"/polls/{poll.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task CreatePoll_MissingToken_Returns401ProblemJson()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/unauthorized", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task CreatePoll_BlankTitle_Returns400ProblemJson()
    {
        using var client = factory.CreateClient();
        var token = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/polls",
            new { title = " ", options = twoOptions });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/invalid-request", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task GetPoll_Existing_Returns200WithDurationSeconds()
    {
        using var client = factory.CreateClient();
        var token = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var created = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });
        var poll = await created.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(poll);

        using var response = await client.GetAsync($"/polls/{poll.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<PollBody>();
        Assert.NotNull(fetched);
        Assert.Equal(poll.Id, fetched.Id);
        Assert.Equal(600, fetched.DurationSeconds);
        Assert.Equal("Lunch spot", fetched.Title);
    }

    [Fact]
    public async Task GetPoll_MissingToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/polls/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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