using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using LivePoll.Api.Metrics;

namespace LivePoll.Api.Tests;

public sealed class MetricsEndpointTests
{
    [Fact]
    public async Task GetMetrics_ReturnsVotesPerSecondTotalActiveOptionsAndBacklog()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);
        var optionA = poll.Options.Single(option => option.Text == "Cafeteria").Id;
        var optionB = poll.Options.Single(option => option.Text == "Food truck").Id;
        var serverNow = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var previousSecond = serverNow - 1000;
        factory.Metrics.TotalVotes = 6;
        factory.Metrics.Backlog = 1;
        factory.Metrics.ActiveEndTimes.Add(poll.EndsAt);
        factory.Metrics.RuvioServerUnixMilliseconds = serverNow;
        factory.Metrics.StreamEntryIds.Add($"{previousSecond}-0");
        factory.Metrics.StreamEntryIds.Add($"{previousSecond + 1}-0");
        factory.Metrics.StreamEntryIds.Add($"{previousSecond + 2}-0");
        factory.Metrics.OptionVotes[(poll.Id, optionA)] = 4;
        factory.Metrics.OptionVotes[(poll.Id, optionB)] = 2;
        factory.ActivePolls.Entries.Add(new ActivePollEntry(poll.Id, poll.EndsAt));

        var token = await ResultsApi.SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal(3, body.GetProperty("votesPerSecond").GetInt32());
        Assert.Equal(6, body.GetProperty("totalVotes").GetInt64());
        Assert.Equal(1, body.GetProperty("activePolls").GetInt32());
        Assert.Equal(1, body.GetProperty("votesWaitingForPostgres").GetInt64());
        var options = body.GetProperty("votesPerOption");
        Assert.Equal(2, options.GetArrayLength());
        AssertOption(options, poll.Id, optionA, "Cafeteria", 4);
        AssertOption(options, poll.Id, optionB, "Food truck", 2);
    }

    [Fact]
    public async Task GetMetrics_MissingToken_Returns401()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("/problems/unauthorized", document.RootElement.GetProperty("type").GetString());
    }

    private static void AssertOption(JsonElement options, Guid pollId, Guid optionId, string text, long votes)
    {
        var match = options.EnumerateArray().Single(option => option.GetProperty("optionId").GetGuid() == optionId);
        Assert.Equal(pollId, match.GetProperty("pollId").GetGuid());
        Assert.Equal(text, match.GetProperty("text").GetString());
        Assert.Equal(votes, match.GetProperty("votes").GetInt64());
    }
}
