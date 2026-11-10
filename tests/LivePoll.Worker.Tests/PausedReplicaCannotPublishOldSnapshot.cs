namespace LivePoll.Worker.Tests;

public sealed class PausedReplicaCannotPublishOldSnapshot
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");

    [Fact(DisplayName = "PausedReplicaCannotPublishOldSnapshot")]
    public async Task Pause_DoesNotPublishTheOldSnapshotOrReplaceANewerOne()
    {
        var world = new PublishWorld();
        world.Lock.NextToken = 2;
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 9, 1)];
        await world.CreatePublisher().PublishOnceAsync();

        var newer = SnapshotJson.Read(Assert.Single(world.Results.Messages).Payload);
        Assert.Equal(2, newer.FencingToken);
        Assert.Equal(9, newer.Options.Single(option => option.OptionId == optionA).Votes);

        world.Lock.NextToken = 1;
        var reads = 0;
        world.Totals.Handler = _ =>
        {
            reads++;
            if (reads == 1)
            {
                world.Lock.Current!.IsHeld = false;
            }

            return Task.FromResult<IReadOnlyList<LivePoll.Worker.LivePollPicture>>(
                [world.Picture(pollId, optionA, optionB, 1, 0)]);
        };

        await world.CreatePublisher().PublishOnceAsync();

        Assert.Equal(1, reads);
        Assert.Equal(9, SnapshotJson.Read(Assert.Single(world.Results.Messages).Payload).Options.Single(option => option.OptionId == optionA).Votes);

        world.Totals.Handler = null;
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 1, 0)];
        world.Lock.NextToken = 1;
        await world.CreatePublisher().PublishOnceAsync();

        var current = SnapshotJson.Read(Assert.Single(world.Results.Messages).Payload);
        Assert.Equal(2, current.FencingToken);
        Assert.Equal(9, current.Options.Single(option => option.OptionId == optionA).Votes);
    }
}