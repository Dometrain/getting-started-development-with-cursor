using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Ruvio.AspNetCore.RateLimiting;

namespace LivePoll.Api.Tests;

public sealed class PublicResultsTests
{
    [Fact]
    public async Task GetResults_NoToken_ReturnsSnapshot()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);
        var optionA = poll.Options.Single(option => option.Text == "Cafeteria").Id;
        var optionB = poll.Options.Single(option => option.Text == "Food truck").Id;
        factory.Metrics.OptionVotes[(poll.Id, optionA)] = 4;
        factory.Metrics.OptionVotes[(poll.Id, optionB)] = 2;
        SetVotesPerSecond(factory, 3);

        using var response = await client.GetAsync($"/polls/{poll.Id}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = await ReadJsonAsync(response);
        AssertSnapshot(document.RootElement, poll.Id, ended: false, votesPerSecond: 3, optionA, 4, optionB, 2);
    }

    [Fact]
    public async Task GetResults_AfterPollEnds_ReturnsEndedSnapshot()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);
        factory.Clock.UtcNow = poll.EndsAt;

        using var response = await client.GetAsync($"/polls/{poll.Id}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.True(document.RootElement.GetProperty("ended").GetBoolean());
        Assert.Equal(poll.Id, document.RootElement.GetProperty("pollId").GetGuid());
    }

    [Fact]
    public async Task GetResults_UnknownPoll_Returns404()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var pollId = Guid.NewGuid();

        using var response = await client.GetAsync($"/polls/{pollId}/results");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = await ReadJsonAsync(response);
        Assert.Equal("/problems/not-found", document.RootElement.GetProperty("type").GetString());
        Assert.Equal($"/polls/{pollId}/results", document.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Stream_NoToken_ReturnsResultsEventWithNoCache()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);
        var optionA = poll.Options.Single(option => option.Text == "Cafeteria").Id;
        var optionB = poll.Options.Single(option => option.Text == "Food truck").Id;
        factory.Metrics.OptionVotes[(poll.Id, optionA)] = 4;
        factory.Metrics.OptionVotes[(poll.Id, optionB)] = 2;
        SetVotesPerSecond(factory, 3);

        using var stream = await ResultsApi.OpenStreamAsync(client, poll.Id);

        Assert.Equal(HttpStatusCode.OK, stream.Response.StatusCode);
        Assert.Equal("text/event-stream", stream.Response.Content.Headers.ContentType?.MediaType);
        Assert.True(stream.Response.Headers.TryGetValues("Cache-Control", out var cacheControl));
        Assert.Contains(cacheControl, value => value.Contains("no-cache", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(await stream.Reader.ReadDataAsync(TimeSpan.FromSeconds(3)));
        AssertSnapshot(document.RootElement, poll.Id, ended: false, votesPerSecond: 3, optionA, 4, optionB, 2);
    }

    [Fact]
    public async Task Stream_AfterPollEnds_StaysAvailable()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);
        factory.Clock.UtcNow = poll.EndsAt;

        using var stream = await ResultsApi.OpenStreamAsync(client, poll.Id);

        Assert.Equal(HttpStatusCode.OK, stream.Response.StatusCode);
        using var document = JsonDocument.Parse(await stream.Reader.ReadDataAsync(TimeSpan.FromSeconds(3)));
        Assert.True(document.RootElement.GetProperty("ended").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("fencingToken", out _));
    }

    [Fact]
    public async Task Stream_UnknownPoll_Returns404()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var pollId = Guid.NewGuid();

        using var response = await client.GetAsync($"/polls/{pollId}/results/stream");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void ResultsRoutes_HaveNoRateLimitPolicy()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var results = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == "/polls/{pollId:guid}/results");
        var stream = Assert.Single(
            endpoints,
            endpoint => endpoint.RoutePattern.RawText == "/polls/{pollId:guid}/results/stream");
        var metrics = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == "/metrics");

        Assert.Empty(results.Metadata.OfType<RuvioRateLimitAttribute>());
        Assert.Empty(stream.Metadata.OfType<RuvioRateLimitAttribute>());
        Assert.Empty(metrics.Metadata.OfType<RuvioRateLimitAttribute>());
    }

    private static void SetVotesPerSecond(ApiWebApplicationFactory factory, int count)
    {
        var serverNow = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var previousSecond = serverNow - 1000;
        factory.Metrics.RuvioServerUnixMilliseconds = serverNow;
        for (var index = 0; index < count; index++)
        {
            factory.Metrics.StreamEntryIds.Add($"{previousSecond + index}-0");
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static void AssertSnapshot(
        JsonElement snapshot,
        Guid pollId,
        bool ended,
        int votesPerSecond,
        Guid optionA,
        long votesA,
        Guid optionB,
        long votesB)
    {
        Assert.Equal(pollId, snapshot.GetProperty("pollId").GetGuid());
        Assert.Equal(ended, snapshot.GetProperty("ended").GetBoolean());
        Assert.Equal(votesPerSecond, snapshot.GetProperty("votesPerSecond").GetInt32());
        Assert.False(snapshot.TryGetProperty("fencingToken", out _));
        var options = snapshot.GetProperty("options");
        Assert.Equal(2, options.GetArrayLength());
        Assert.Equal(votesA, OptionVotes(options, optionA));
        Assert.Equal(votesB, OptionVotes(options, optionB));
        Assert.Equal("Cafeteria", OptionText(options, optionA));
        Assert.Equal("Food truck", OptionText(options, optionB));
    }

    private static long OptionVotes(JsonElement options, Guid optionId) =>
        options.EnumerateArray().Single(option => option.GetProperty("optionId").GetGuid() == optionId)
            .GetProperty("votes")
            .GetInt64();

    private static string? OptionText(JsonElement options, Guid optionId) =>
        options.EnumerateArray().Single(option => option.GetProperty("optionId").GetGuid() == optionId)
            .GetProperty("text")
            .GetString();
}
