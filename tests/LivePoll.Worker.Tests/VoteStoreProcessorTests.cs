using LivePoll.Worker.Voting;

namespace LivePoll.Worker.Tests;

public sealed class VoteStoreProcessorTests
{
    private readonly Guid pollId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly Guid optionId = Guid.Parse("ffffffff-0000-1111-2222-333333333333");

    [Fact]
    public async Task ProcessBatch_AcksOnlyAfterSuccessfulStore()
    {
        var delivery = new VoteDelivery("10-0", pollId, "ada", optionId, DeliveryCount: 1);
        var store = new FakeVoteStore { FailNextStore = true };
        var source = new FakeVoteDeliverySource([delivery]) { Store = store };
        var backlog = new FakeBacklogCounter();
        var processor = new VoteStoreProcessor(source, store, backlog);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessBatchAsync(CancellationToken.None));

        Assert.Empty(source.Acknowledged);
        Assert.Equal(0, backlog.DecrementCount);

        store.FailNextStore = false;
        source.Queued = [delivery];

        await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(["10-0"], source.Acknowledged);
        Assert.Equal([("ada", optionId)], store.StoredVotes);
        Assert.True(source.AckedAfterStore);
        Assert.Equal(1, backlog.DecrementCount);
    }

    [Fact]
    public async Task ProcessBatch_RedeliveryOfExistingPollAndUsername_DoesNotInsertAgain()
    {
        var first = new VoteDelivery("20-0", pollId, "ada", optionId, DeliveryCount: 1);
        var redelivery = new VoteDelivery("20-0", pollId, "ada", optionId, DeliveryCount: 2);
        var source = new FakeVoteDeliverySource([first]);
        var store = new FakeVoteStore();
        var backlog = new FakeBacklogCounter();
        var processor = new VoteStoreProcessor(source, store, backlog);

        await processor.ProcessBatchAsync(CancellationToken.None);
        source.Queued = [redelivery];
        await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal([("ada", optionId)], store.StoredVotes);
        Assert.Equal(2, store.TryStoreCallCount);
        Assert.Equal(["20-0", "20-0"], source.Acknowledged);
        Assert.Equal(1, backlog.DecrementCount);
    }

    [Fact]
    public async Task ProcessBatch_DecrementsBacklogOnceForThatStore()
    {
        var delivery = new VoteDelivery("30-0", pollId, "grace", optionId, DeliveryCount: 1);
        var source = new FakeVoteDeliverySource([delivery]);
        var store = new FakeVoteStore();
        var backlog = new FakeBacklogCounter();
        var processor = new VoteStoreProcessor(source, store, backlog);

        await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, backlog.DecrementCount);
        Assert.Equal(["30-0"], source.Acknowledged);
    }

    [Fact]
    public async Task ProcessBatch_MissingOptionRow_IsKeptAsideAndAcknowledged()
    {
        var delivery = new VoteDelivery("40-0", pollId, "ada", optionId, DeliveryCount: 1);
        var source = new FakeVoteDeliverySource([delivery]);
        var store = new FakeVoteStore { MissingOption = true };
        var backlog = new FakeBacklogCounter();
        var processor = new VoteStoreProcessor(source, store, backlog);

        await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal([delivery], source.KeptAside);
        Assert.Equal(["40-0"], source.Acknowledged);
        Assert.Equal(0, backlog.DecrementCount);
        Assert.Empty(store.StoredVotes);
    }
}