namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class ResultsEventsFlushImmediately(ResultsFixture stack)
{
    [Fact(DisplayName = "ResultsEventsFlushImmediately")]
    public async Task ResultsEvent_ReachesTheClientWhileTheStreamIsOpen()
    {
        await stack.EnsureReplicasAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var token = await PublicSite.SignInAsync(client, "flush-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);
        await using var stream = await LiveResultsStream.OpenAsync(client, poll.Id, CancellationToken.None);

        Assert.Equal(System.Net.HttpStatusCode.OK, stream.StatusCode);
        Assert.Equal("text/event-stream", stream.ContentType);
        Assert.Null(stream.ContentLength);

        _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        Assert.True(stream.StillOpen);

        _ = await PublicSite.VoteAsync(client, token, poll.Id, poll.FirstOptionId, CancellationToken.None);
        var update = await stream.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(3), CancellationToken.None);

        Assert.True(stream.StillOpen);
        Assert.True(LiveResultsStream.Votes(update, poll.FirstOptionId) >= 1);
    }
}