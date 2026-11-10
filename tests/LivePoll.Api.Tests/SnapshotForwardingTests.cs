using System.Text.Json;

using LivePoll.Api.LiveResults;

using Microsoft.Extensions.DependencyInjection;

namespace LivePoll.Api.Tests;

public sealed class SnapshotForwardingTests
{
    [Fact]
    public async Task Stream_DropsSmallerFencingToken()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);
        var optionA = poll.Options.Single(option => option.Text == "Cafeteria").Id;
        var optionB = poll.Options.Single(option => option.Text == "Food truck").Id;
        var hub = factory.Services.GetRequiredService<ResultsSnapshotHub>();

        using var stream = await ResultsApi.OpenStreamAsync(client, poll.Id);
        _ = await stream.Reader.ReadDataAsync(TimeSpan.FromSeconds(3));

        factory.Clock.UtcNow += ResultsSnapshotHub.MinimumGap;
        Assert.True(hub.AcceptPublishedPayload(Payload(poll.Id, optionA, optionB, 4, 2, fencingToken: 5)));
        using (var accepted = JsonDocument.Parse(await stream.Reader.ReadDataAsync(TimeSpan.FromSeconds(3))))
        {
            Assert.Equal(4, Votes(accepted.RootElement, optionA));
            Assert.False(accepted.RootElement.TryGetProperty("fencingToken", out _));
        }

        factory.Clock.UtcNow += ResultsSnapshotHub.MinimumGap;
        Assert.False(hub.AcceptPublishedPayload(Payload(poll.Id, optionA, optionB, 99, 1, fencingToken: 3)));
        Assert.True(hub.AcceptPublishedPayload(Payload(poll.Id, optionA, optionB, 7, 1, fencingToken: 6)));

        using var next = JsonDocument.Parse(await stream.Reader.ReadDataAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(7, Votes(next.RootElement, optionA));
        Assert.False(next.RootElement.TryGetProperty("fencingToken", out _));
    }

    [Fact]
    public async Task Stream_ForwardsAtMostOneSnapshotPerViewerPerSecond()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(firstClient);
        var optionA = poll.Options.Single(option => option.Text == "Cafeteria").Id;
        var optionB = poll.Options.Single(option => option.Text == "Food truck").Id;
        var hub = factory.Services.GetRequiredService<ResultsSnapshotHub>();

        using var first = await ResultsApi.OpenStreamAsync(firstClient, poll.Id);
        _ = await first.Reader.ReadDataAsync(TimeSpan.FromSeconds(3));

        Assert.True(hub.AcceptPublishedPayload(Payload(poll.Id, optionA, optionB, 5, 1, fencingToken: 1)));

        factory.Clock.UtcNow += ResultsSnapshotHub.MinimumGap;
        using var second = await ResultsApi.OpenStreamAsync(secondClient, poll.Id);
        _ = await second.Reader.ReadDataAsync(TimeSpan.FromSeconds(3));

        Assert.True(hub.AcceptPublishedPayload(Payload(poll.Id, optionA, optionB, 9, 1, fencingToken: 2)));
        using (var firstEvent = JsonDocument.Parse(await first.Reader.ReadDataAsync(TimeSpan.FromSeconds(3))))
        {
            Assert.Equal(9, Votes(firstEvent.RootElement, optionA));
        }

        factory.Clock.UtcNow += ResultsSnapshotHub.MinimumGap;
        Assert.True(hub.AcceptPublishedPayload(Payload(poll.Id, optionA, optionB, 8, 1, fencingToken: 3)));
        using var secondEvent = JsonDocument.Parse(await second.Reader.ReadDataAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(8, Votes(secondEvent.RootElement, optionA));
    }

    private static string Payload(Guid pollId, Guid optionA, Guid optionB, long votesA, long votesB, long fencingToken) =>
        ResultsApi.PublishedSnapshot(pollId, optionA, optionB, votesA, votesB, votesPerSecond: 3, fencingToken);

    private static long Votes(JsonElement snapshot, Guid optionId) =>
        snapshot.GetProperty("options").EnumerateArray()
            .Single(option => option.GetProperty("optionId").GetGuid() == optionId)
            .GetProperty("votes")
            .GetInt64();
}
