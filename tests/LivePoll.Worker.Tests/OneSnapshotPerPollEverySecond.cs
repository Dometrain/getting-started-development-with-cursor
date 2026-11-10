namespace LivePoll.Worker.Tests;

public sealed class OneSnapshotPerPollEverySecond
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");

    [Fact(DisplayName = "OneSnapshotPerPollEverySecond")]
    public async Task Burst_LeavesAtLeastOneSecondBetweenSnapshots()
    {
        var world = new PublishWorld();
        var publisher = world.CreatePublisher();
        var start = world.Clock.UtcNow;

        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 1, 0)];
        await publisher.PublishOnceAsync();

        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 4, 1)];
        await publisher.PublishOnceAsync();

        world.Clock.UtcNow = start.AddMilliseconds(400);
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 6, 2)];
        await publisher.PublishOnceAsync();

        Assert.Single(world.Results.Messages);

        world.Clock.UtcNow = start.AddSeconds(1);
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 8, 3)];
        await publisher.PublishOnceAsync();

        Assert.Equal(2, world.Results.Messages.Count);
        Assert.True(world.Results.Messages[1].At - world.Results.Messages[0].At >= TimeSpan.FromSeconds(1));
        Assert.Equal(1, SnapshotJson.Read(world.Results.Messages[0].Payload).Options.Single(option => option.OptionId == optionA).Votes);
        Assert.Equal(8, SnapshotJson.Read(world.Results.Messages[1].Payload).Options.Single(option => option.OptionId == optionA).Votes);
    }
}