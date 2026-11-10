namespace LivePoll.Worker.Tests;

public sealed class SnapshotLoop_WaitsBetweenOptionReads
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");

    [Fact]
    public async Task WhileTheLeaseIsHeld_TotalsAreReadOncePerSnapshotInterval()
    {
        var world = new PublishWorld();
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 1, 0)];
        var service = new SnapshotPublishService(world.CreatePublisher());

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, world.Totals.Reads);
    }
}