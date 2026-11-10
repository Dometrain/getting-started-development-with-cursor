using LivePoll.Application;

namespace LivePoll.Worker.Tests;

public sealed class LiveSnapshotContents
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");

    [Fact]
    public async Task Publish_CarriesLiveOptionTotalsAndStreamVotesPerSecond()
    {
        var world = new PublishWorld();
        var serverNow = world.Clock.UtcNow.ToUnixTimeMilliseconds();
        var previousSecond = serverNow - (serverNow % 1000) - 1000;
        var entryIds = Enumerable.Range(0, 3).Select(index => $"{previousSecond + index}-0").ToList();
        world.Totals.Pictures =
        [
            world.Picture(pollId, optionA, optionB, 5, 2, entryIds, serverNow),
        ];

        await world.CreatePublisher().PublishOnceAsync();

        var snapshot = SnapshotJson.Read(Assert.Single(world.Results.Messages).Payload);
        Assert.Equal(pollId, snapshot.PollId);
        Assert.False(snapshot.Ended);
        Assert.Equal(1, snapshot.FencingToken);
        Assert.Equal(5, snapshot.Options.Single(option => option.OptionId == optionA).Votes);
        Assert.Equal("Cafeteria", snapshot.Options.Single(option => option.OptionId == optionA).Text);
        Assert.Equal(2, snapshot.Options.Single(option => option.OptionId == optionB).Votes);
        Assert.Equal(VotesPerSecond.FromStreamEntryIds(entryIds, serverNow), snapshot.VotesPerSecond);
        Assert.Equal(3, snapshot.VotesPerSecond);
    }
}