using LivePoll.Worker;

namespace LivePoll.Worker.Tests;

public sealed class OnlyLeaseHolderPublishes
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");

    [Fact(DisplayName = "OnlyLeaseHolderPublishes")]
    public async Task TwoWorkers_OnlyTheHolderPublishes()
    {
        var world = new PublishWorld();
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 4, 1)];
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        world.Totals.Handler = async cancellationToken =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }

            return world.Totals.Pictures;
        };

        var holder = world.CreatePublisher();
        var other = world.CreatePublisher();
        var publishing = holder.PublishOnceAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await other.PublishOnceAsync();
        release.TrySetResult();
        await publishing;

        var snapshot = Assert.Single(world.Results.Messages);
        Assert.Equal(pollId, SnapshotJson.Read(snapshot.Payload).PollId);
        Assert.Equal(2, world.Lock.Leases.Count);
        Assert.All(world.Lock.Leases, lease => Assert.True(lease < TimeSpan.FromSeconds(1)));
    }
}