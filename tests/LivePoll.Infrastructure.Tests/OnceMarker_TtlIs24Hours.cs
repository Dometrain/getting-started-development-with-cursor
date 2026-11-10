using LivePoll.Infrastructure.Ruvio;

namespace LivePoll.Infrastructure.Tests;

[Collection(RuvioContainerGroup.Name)]
public sealed class OnceMarker_TtlIs24Hours(RuvioFixture fixture)
{
    [Fact]
    public async Task HoldsFor24Hours_AndRejectsASecondVote()
    {
        await fixture.ResetAsync();
        var marker = new RuvioOneVoteMarker(fixture.Client);
        var pollId = Guid.NewGuid();
        var key = RuvioKeys.Once(pollId, "ada");

        Assert.True(await marker.TryAcceptAsync(pollId, "ada", CancellationToken.None));

        var firstTtl = await fixture.Client.GetTimeToLiveMillisecondsAsync(key);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        Assert.False(await marker.TryAcceptAsync(pollId, "ada", CancellationToken.None));
        var secondTtl = await fixture.Client.GetTimeToLiveMillisecondsAsync(key);

        Assert.InRange(firstTtl, RuvioKeys.OneVoteTtlMilliseconds - 5_000, RuvioKeys.OneVoteTtlMilliseconds);
        Assert.True(secondTtl <= firstTtl);
        Assert.InRange(secondTtl, RuvioKeys.OneVoteTtlMilliseconds - 10_000, firstTtl);
    }
}
