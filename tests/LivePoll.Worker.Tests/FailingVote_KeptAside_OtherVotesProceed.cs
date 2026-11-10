using LivePoll.Worker.Voting;

namespace LivePoll.Worker.Tests;

public sealed class FailingVote_KeptAside_OtherVotesProceed
{
    private readonly Guid pollId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid badOptionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid goodOptionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task DeliveryCountGreaterThanThree_IsAppendedToSideStreamAndAcked_AndLaterGoodVoteIsStored()
    {
        var failing = new VoteDelivery("1-0", pollId, "broken", badOptionId, DeliveryCount: 4);
        var good = new VoteDelivery("2-0", pollId, "ada", goodOptionId, DeliveryCount: 1);
        var source = new FakeVoteDeliverySource([failing, good]);
        var store = new FakeVoteStore();
        var backlog = new FakeBacklogCounter();
        var processor = new VoteStoreProcessor(source, store, backlog);

        await processor.ProcessBatchAsync(CancellationToken.None);

        var aside = Assert.Single(source.KeptAside);
        Assert.Equal(failing.EntryId, aside.EntryId);
        Assert.Contains(failing.EntryId, source.Acknowledged);
        Assert.DoesNotContain(failing.EntryId, store.StoredEntryKeys);
        Assert.Contains(("ada", goodOptionId), store.StoredVotes);
        Assert.Contains(good.EntryId, source.Acknowledged);
        Assert.Equal(1, backlog.DecrementCount);
    }
}